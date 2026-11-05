using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.PartnerDispatch;
using QdratNew.ViewModels.Partner.PerformanceIndicator;

namespace QdratNew.Services.PerformanceIndicator.PartnerScope
{
    // APD-S4.2: كل قراءة وكتابة هنا مقيدة بـ OwnerPartnerId == partnerId وبطلاب فروع هذه المدرسة.
    // null / NotFound لأي اختبار أو طالب خارج النطاق، فلا يظهر للمستخدم هل يوجد عند غيره.
    public interface IPartnerPerformanceExamService
    {
        Task<PartnerPerformanceIndexVm> GetIndexAsync(int partnerId, bool showHidden, bool canManage, CancellationToken ct = default);

        Task<PartnerPerformanceStudentsVm?> GetStudentsAsync(int partnerId, int examId, CancellationToken ct = default);

        Task<PartnerPerformanceStudentQuestionsVm?> GetStudentQuestionsAsync(int partnerId, int examId, int studentId, CancellationToken ct = default);

        Task<PartnerPerformanceBatchReportVm?> GetBatchReportAsync(int partnerId, int examId, int batchId, CancellationToken ct = default);

        // يضيف الطالب أو يعيد له الوقت، ولا يحذف أي نتيجة أو إجابة
        Task<PartnerPerformanceResendOutcome> ResendToStudentAsync(int partnerId, int examId, int studentId, CancellationToken ct = default);

        // إخفاء من قوائم المدرسة فقط: لا يؤثر على الطلاب. false = لا تملكه.
        Task<bool> SetHiddenAsync(int partnerId, int examId, bool hidden, string? userId, CancellationToken ct = default);
    }

    public class PartnerPerformanceExamService : IPartnerPerformanceExamService
    {
        private const string ActiveEnrollmentStatus = "Active";

        private readonly ApplicationDbContext _context;
        private readonly IAdvancedNotificationService _notificationService;
        private readonly ILogger<PartnerPerformanceExamService> _logger;

        public PartnerPerformanceExamService(
            ApplicationDbContext context,
            IAdvancedNotificationService notificationService,
            ILogger<PartnerPerformanceExamService> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<PartnerPerformanceIndexVm> GetIndexAsync(int partnerId, bool showHidden, bool canManage, CancellationToken ct = default)
        {
            var exams = await _context.PerformanceIndicatorExams.AsNoTracking()
                .Where(e => e.OwnerPartnerId == partnerId && e.IsArchived == showHidden)
                .OrderByDescending(e => e.CreatedAt)
                .ThenByDescending(e => e.Id)
                .Select(e => new PartnerPerformanceExamRowVm
                {
                    Id = e.Id,
                    Title = e.Title,
                    CurriculumTitle = e.Curriculum.Title,
                    ReferenceCode = e.ReferenceCode,
                    IsOnline = e.IsOnline,
                    StartAt = e.StartAt,
                    EndAt = e.EndAt,
                    QuestionsCount = e.TotalQuestions,
                    StudentsCount = e.ExamStudents.Count(s => s.Student.Branch.PartnerId == partnerId),
                    CompletedCount = e.ExamStudents.Count(s => s.IsCompleted && s.Student.Branch.PartnerId == partnerId)
                })
                .ToListAsync(ct);

            var batches = (await _context.PerformanceIndicatorExamToBatch.AsNoTracking()
                    .Where(l => l.PerformanceIndicatorExam.OwnerPartnerId == partnerId && l.PerformanceIndicatorExam.IsArchived == showHidden)
                    .Select(l => new { l.PerformanceIndicatorExamId, l.Batch.Name })
                    .ToListAsync(ct))
                .ToLookup(l => l.PerformanceIndicatorExamId, l => l.Name);

            foreach (var exam in exams)
                exam.BatchNames = batches[exam.Id].OrderBy(n => n).ToList();

            return new PartnerPerformanceIndexVm
            {
                ShowHidden = showHidden,
                CanManage = canManage,
                HiddenCount = await _context.PerformanceIndicatorExams.AsNoTracking()
                    .CountAsync(e => e.OwnerPartnerId == partnerId && e.IsArchived, ct),
                Exams = exams
            };
        }

        public async Task<PartnerPerformanceStudentsVm?> GetStudentsAsync(int partnerId, int examId, CancellationToken ct = default)
        {
            var report = await PerformanceExamResultsBuilder.BuildStudentsReportAsync(_context, examId, partnerId, ct);
            if (report == null)
                return null;

            var header = await _context.PerformanceIndicatorExams.AsNoTracking()
                .Where(e => e.Id == examId)
                .Select(e => new { e.ReferenceCode, e.IsOnline, e.IsArchived })
                .FirstAsync(ct);

            var notAssigned = await _context.StudentBatchEnrollments.AsNoTracking()
                .Where(e =>
                    e.Status == ActiveEnrollmentStatus &&
                    e.Student.IsActiveForLearning &&
                    e.Student.Branch.PartnerId == partnerId &&
                    _context.PerformanceIndicatorExamToBatch.Any(l => l.PerformanceIndicatorExamId == examId && l.BatchId == e.BatchId) &&
                    !_context.PerformanceIndicatorExamStudents.Any(s => s.PerformanceIndicatorExamId == examId && s.StudentId == e.StudentID))
                .Select(e => new { e.StudentID, e.Student.FullName })
                .Distinct()
                .OrderBy(s => s.FullName)
                .ToListAsync(ct);

            return new PartnerPerformanceStudentsVm
            {
                Report = report,
                ReferenceCode = header.ReferenceCode,
                IsOnline = header.IsOnline,
                IsHidden = header.IsArchived,
                Batches = await LoadExamBatchesAsync(partnerId, examId, ct),
                NotAssigned = notAssigned
                    .Select(s => new PartnerPerformanceStudentVm { StudentId = s.StudentID, FullName = s.FullName })
                    .ToList()
            };
        }

        public async Task<PartnerPerformanceStudentQuestionsVm?> GetStudentQuestionsAsync(int partnerId, int examId, int studentId, CancellationToken ct = default)
        {
            var status = await _context.PerformanceIndicatorExamStudents.AsNoTracking()
                .Where(s =>
                    s.PerformanceIndicatorExamId == examId &&
                    s.StudentId == studentId &&
                    s.PerformanceIndicatorExam.OwnerPartnerId == partnerId &&
                    s.Student.Branch.PartnerId == partnerId)
                .Select(s => new { s.IsCompleted })
                .FirstOrDefaultAsync(ct);

            if (status == null)
                return null;

            if (!status.IsCompleted)
                return new PartnerPerformanceStudentQuestionsVm { StudentCompleted = false };

            var report = await PerformanceExamResultsBuilder.BuildStudentQuestionsAsync(_context, examId, studentId, partnerId, ct);

            return report == null
                ? null
                : new PartnerPerformanceStudentQuestionsVm { Report = report, StudentCompleted = true };
        }

        public async Task<PartnerPerformanceBatchReportVm?> GetBatchReportAsync(int partnerId, int examId, int batchId, CancellationToken ct = default)
        {
            var batches = await LoadExamBatchesAsync(partnerId, examId, ct);
            var batch = batches.FirstOrDefault(b => b.BatchId == batchId);
            if (batch == null)
                return null;

            var report = await PerformanceExamResultsBuilder.BuildStudentsReportAsync(_context, examId, partnerId, ct);
            if (report == null)
                return null;

            var batchStudents = (await _context.StudentBatchEnrollments.AsNoTracking()
                    .Where(e => e.BatchId == batchId && e.Student.Branch.PartnerId == partnerId)
                    .Select(e => e.StudentID)
                    .ToListAsync(ct))
                .ToHashSet();

            var students = report.Students.Where(s => batchStudents.Contains(s.StudentId)).ToList();
            var completed = students.Where(s => s.IsCompleted).ToList();

            return new PartnerPerformanceBatchReportVm
            {
                ExamId = examId,
                ExamTitle = report.ExamTitle,
                CurriculumTitle = report.CurriculumTitle,
                BatchId = batchId,
                BatchName = batch.Name,
                PassPercent = report.PassPercent,
                TotalStudents = students.Count,
                CompletedStudents = completed.Count,
                InProgressStudents = students.Count(s => !s.IsCompleted && s.StartedAt.HasValue),
                NotStartedStudents = students.Count(s => !s.IsCompleted && !s.StartedAt.HasValue),
                PassedStudents = completed.Count(s => s.Passed),
                NeedPlanStudents = completed.Count(s => !s.Passed),
                AverageScore = completed.Count == 0 ? 0 : Math.Round(completed.Average(s => s.ScorePercent ?? 0), 1),
                Sections = completed
                    .SelectMany(s => s.SectionScores)
                    .GroupBy(s => s.SectionTitle)
                    .Select(g => new PartnerPerformanceSectionStatVm
                    {
                        SectionTitle = g.Key,
                        AverageScore = Math.Round(g.Average(x => x.ScorePercent), 1),
                        BelowPassCount = g.Count(x => x.ScorePercent < report.PassPercent)
                    })
                    .OrderBy(s => s.AverageScore)
                    .ToList(),
                Students = students
                    .OrderByDescending(s => s.IsCompleted)
                    .ThenByDescending(s => s.ScorePercent)
                    .ThenBy(s => s.StudentName)
                    .ToList(),
                Batches = batches
            };
        }

        public async Task<PartnerPerformanceResendOutcome> ResendToStudentAsync(int partnerId, int examId, int studentId, CancellationToken ct = default)
        {
            var exam = await _context.PerformanceIndicatorExams.AsNoTracking()
                .Where(e => e.Id == examId && e.OwnerPartnerId == partnerId)
                .Select(e => new { e.Title })
                .FirstOrDefaultAsync(ct);

            if (exam == null)
                return PartnerPerformanceResendOutcome.NotFound;

            var isPartnerActiveStudent = await _context.Students.AsNoTracking()
                .AnyAsync(s => s.StudentID == studentId && s.IsActiveForLearning && s.Branch.PartnerId == partnerId, ct);

            if (!isPartnerActiveStudent)
                return PartnerPerformanceResendOutcome.NotEligible;

            var current = await _context.PerformanceIndicatorExamStudents.AsNoTracking()
                .Where(s => s.PerformanceIndicatorExamId == examId && s.StudentId == studentId)
                .Select(s => new { s.IsCompleted, s.StartedAt })
                .FirstOrDefaultAsync(ct);

            PartnerPerformanceResendOutcome outcome;

            if (current == null)
            {
                var inExamBatch = await _context.StudentBatchEnrollments.AsNoTracking()
                    .AnyAsync(e =>
                        e.StudentID == studentId &&
                        e.Status == ActiveEnrollmentStatus &&
                        _context.PerformanceIndicatorExamToBatch.Any(l => l.PerformanceIndicatorExamId == examId && l.BatchId == e.BatchId), ct);

                if (!inExamBatch)
                    return PartnerPerformanceResendOutcome.NotEligible;

                // لا قيد فريد على (الاختبار، الطالب)، فالإدراج ذري حتى لا تُنشئ الضغطة المزدوجة صفين
                var inserted = await _context.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (SELECT 1 FROM PerformanceIndicatorExamStudents WITH (UPDLOCK, HOLDLOCK)
               WHERE PerformanceIndicatorExamId = {examId} AND StudentId = {studentId})
    INSERT INTO PerformanceIndicatorExamStudents (PerformanceIndicatorExamId, StudentId, IsCompleted)
    VALUES ({examId}, {studentId}, 0);", ct);

                outcome = inserted > 0 ? PartnerPerformanceResendOutcome.Added : PartnerPerformanceResendOutcome.AlreadyAvailable;
            }
            else if (current.IsCompleted)
            {
                // إعادة الحل تكتب فوق إجابات الطالب ونتيجته، والنتيجة لا تُحذف
                return PartnerPerformanceResendOutcome.AlreadyCompleted;
            }
            else if (current.StartedAt == null)
            {
                return PartnerPerformanceResendOutcome.AlreadyAvailable;
            }
            else
            {
                // بدأ ولم يكمل: يعود له الوقت كاملًا، وإجاباته السابقة باقية
                var reopened = await _context.PerformanceIndicatorExamStudents
                    .Where(s => s.PerformanceIndicatorExamId == examId && s.StudentId == studentId && !s.IsCompleted && s.StartedAt != null)
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.StartedAt, (DateTime?)null), ct);

                if (reopened == 0)
                    return PartnerPerformanceResendOutcome.AlreadyCompleted;

                outcome = PartnerPerformanceResendOutcome.Reopened;
            }

            if (outcome != PartnerPerformanceResendOutcome.AlreadyAvailable)
                await NotifyStudentAsync(partnerId, exam.Title, studentId, ct);

            return outcome;
        }

        public async Task<bool> SetHiddenAsync(int partnerId, int examId, bool hidden, string? userId, CancellationToken ct = default)
        {
            var nowUtc = DateTime.UtcNow;

            var owned = await _context.PerformanceIndicatorExams.AsNoTracking()
                .AnyAsync(e => e.Id == examId && e.OwnerPartnerId == partnerId, ct);

            if (!owned)
                return false;

            await _context.PerformanceIndicatorExams
                .Where(e => e.Id == examId && e.OwnerPartnerId == partnerId)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(e => e.IsArchived, hidden)
                    .SetProperty(e => e.ArchivedAt, hidden ? nowUtc : (DateTime?)null)
                    .SetProperty(e => e.ArchivedByUserId, hidden ? userId : null), ct);

            return true;
        }

        // دفعات الاختبار التي تخص هذه المدرسة. قائمة فارغة: لا يملك الاختبار.
        private Task<List<PartnerPerformanceBatchVm>> LoadExamBatchesAsync(int partnerId, int examId, CancellationToken ct)
            => _context.PerformanceIndicatorExamToBatch.AsNoTracking()
                .Where(l =>
                    l.PerformanceIndicatorExamId == examId &&
                    l.PerformanceIndicatorExam.OwnerPartnerId == partnerId &&
                    l.Batch.Branch.PartnerId == partnerId)
                .OrderBy(l => l.Batch.Name)
                .Select(l => new PartnerPerformanceBatchVm { BatchId = l.BatchId, Name = l.Batch.Name })
                .ToListAsync(ct);

        private async Task NotifyStudentAsync(int partnerId, string title, int studentId, CancellationToken ct)
        {
            try
            {
                await _notificationService.SendToStudentsAsync(
                    new List<int> { studentId },
                    $"📊 أُعيد إرسال اختبار مؤشر الأداء «{title}» إليك. يمكنك الدخول وبدء الحل.",
                    NotificationCategory.Exam,
                    PartnerPerformanceDispatchService.StudentDashboardUrl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "APD performance indicator resend notification failed for partner {PartnerId}", partnerId);
            }
        }
    }
}
