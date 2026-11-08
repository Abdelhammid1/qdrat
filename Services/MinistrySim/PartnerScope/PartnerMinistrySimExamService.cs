using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Partner.MinistrySim;
using QdratNew.ViewModels.Reports;

namespace QdratNew.Services.MinistrySim.PartnerScope
{
    // APD-S5.2: كل قراءة وكتابة هنا مقيدة بتكليفات OwnerPartnerId == partnerId وبطلاب المدرسة بقاعدة IMinistrySimOwnershipQuery.
    // null / false لأي اختبار أو طالب أو محاولة خارج النطاق، فلا يظهر للمستخدم هل يوجد عند غيره.
    public interface IPartnerMinistrySimExamService
    {
        Task<PartnerMinistrySimIndexVm> GetIndexAsync(int partnerId, bool showHidden, bool canManage, CancellationToken ct = default);

        // batchId: دفعة مُسنَدة للمدرسة في هذا الاختبار، أو null لكل طلابها فيه
        Task<PartnerMinistrySimStudentsVm?> GetStudentsAsync(int partnerId, int examId, int? batchId, CancellationToken ct = default);

        Task<bool> OwnsStudentAsync(int partnerId, int examId, int studentId, CancellationToken ct = default);

        // ملاحظات تقرير ولي الأمر على محاولة يملكها هذا الشريك. false = خارج النطاق.
        Task<bool> SaveParentReportNoteAsync(int partnerId, int attemptId, string? note, CancellationToken ct = default);

        Task<ReportLetterhead> GetLetterheadAsync(int partnerId, CancellationToken ct = default);

        // إخفاء الاختبار من قائمة المدرسة فقط: لا يؤثر على الطلاب. false = لا تكليف لها فيه.
        Task<bool> SetHiddenAsync(int partnerId, int examId, bool hidden, CancellationToken ct = default);
    }

    public class PartnerMinistrySimExamService : IPartnerMinistrySimExamService
    {
        public const string CollaborationNote = "بالتعاون مع معهد القدرات";

        private const string ActiveEnrollmentStatus = "Active";
        private const string BatchSourceType = "Batch";

        private readonly ApplicationDbContext _context;
        private readonly IMinistrySimOwnershipQuery _ownership;

        public PartnerMinistrySimExamService(ApplicationDbContext context, IMinistrySimOwnershipQuery ownership)
        {
            _context = context;
            _ownership = ownership;
        }

        public async Task<PartnerMinistrySimIndexVm> GetIndexAsync(int partnerId, bool showHidden, bool canManage, CancellationToken ct = default)
        {
            var batchRows = await _context.MinistrySimExamAssignmentsToBatches.AsNoTracking()
                .Where(a => a.OwnerPartnerId == partnerId)
                .Select(a => new
                {
                    a.MinistrySimExamId,
                    a.BatchId,
                    BatchName = a.Batch.Name,
                    a.IsOnline,
                    a.ReferenceCode,
                    a.VisibleFrom,
                    a.AssignedAt,
                    Visible = a.HiddenByOwnerAt == null
                })
                .ToListAsync(ct);

            // تجميع في SQL: صفوف تكليف الطلاب قد تكون بالآلاف
            var studentGroups = await _context.MinistrySimExamAssignmentsToStudents.AsNoTracking()
                .Where(a => a.OwnerPartnerId == partnerId)
                .GroupBy(a => a.MinistrySimExamId)
                .Select(g => new
                {
                    ExamId = g.Key,
                    Individual = g.Sum(a => a.SourceType == BatchSourceType ? 0 : 1),
                    AnyVisible = g.Max(a => a.HiddenByOwnerAt == null ? 1 : 0),
                    LastAssignedAt = g.Max(a => a.AssignedAt)
                })
                .ToListAsync(ct);

            // عدد الطلاب بنفس قاعدة صفحة الطلاب (يشمل من انضم لدفعة مُسنَدة بعد إسنادها)
            var studentCounts = await _ownership.PartnerStudentPairs(partnerId)
                .GroupBy(p => p.ExamId)
                .Select(g => new { ExamId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ExamId, x => x.Count, ct);

            var completed = await _ownership.PartnerAttempts(partnerId)
                .Where(a => a.IsCompleted)
                .GroupBy(a => a.MinistrySimExamId)
                .Select(g => new { ExamId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ExamId, x => x.Count, ct);

            var examIds = batchRows.Select(b => b.MinistrySimExamId)
                .Concat(studentGroups.Select(s => s.ExamId))
                .Distinct()
                .ToList();

            if (examIds.Count == 0)
                return new PartnerMinistrySimIndexVm { ShowHidden = showHidden, CanManage = canManage };

            var exams = await _context.MinistrySimExams.AsNoTracking()
                .Where(e => EF.Constant(examIds).Contains(e.Id))
                .Select(e => new { e.Id, e.Title, CourseName = e.Course.Name })
                .ToListAsync(ct);

            var batchesByExam = batchRows.ToLookup(b => b.MinistrySimExamId);
            var studentsByExam = studentGroups.ToDictionary(s => s.ExamId);

            // الاختبار مخفي عندما تكون كل صفوف المدرسة فيه مخفية؛ إسناد جديد بعد الإخفاء يعيد ظهوره
            bool IsVisible(int examId) =>
                batchesByExam[examId].Any(b => b.Visible) ||
                (studentsByExam.TryGetValue(examId, out var s) && s.AnyVisible == 1);

            var rows = exams
                .Select(e =>
                {
                    var batches = batchesByExam[e.Id].OrderBy(b => b.BatchName).ToList();
                    studentsByExam.TryGetValue(e.Id, out var students);

                    return new
                    {
                        Visible = IsVisible(e.Id),
                        Row = new PartnerMinistrySimExamRowVm
                        {
                            ExamId = e.Id,
                            Title = e.Title,
                            CourseName = e.CourseName,
                            Batches = batches
                                .Select(b => new PartnerMinistrySimBatchVm
                                {
                                    BatchId = b.BatchId,
                                    Name = b.BatchName,
                                    IsOnline = b.IsOnline,
                                    ReferenceCode = b.ReferenceCode,
                                    VisibleFrom = b.VisibleFrom
                                })
                                .ToList(),
                            IndividualCount = students?.Individual ?? 0,
                            StudentsCount = studentCounts.GetValueOrDefault(e.Id),
                            CompletedCount = completed.GetValueOrDefault(e.Id),
                            LastAssignedAt = new[] { students?.LastAssignedAt ?? DateTime.MinValue }
                                .Concat(batches.Select(b => b.AssignedAt))
                                .Max()
                        }
                    };
                })
                .ToList();

            return new PartnerMinistrySimIndexVm
            {
                ShowHidden = showHidden,
                CanManage = canManage,
                HiddenCount = rows.Count(r => !r.Visible),
                Exams = rows
                    .Where(r => r.Visible != showHidden)
                    .Select(r => r.Row)
                    .OrderByDescending(r => r.LastAssignedAt)
                    .ThenByDescending(r => r.ExamId)
                    .ToList()
            };
        }

        public async Task<PartnerMinistrySimStudentsVm?> GetStudentsAsync(int partnerId, int examId, int? batchId, CancellationToken ct = default)
        {
            var hasAssignment =
                await _context.MinistrySimExamAssignmentsToBatches.AsNoTracking().AnyAsync(a => a.MinistrySimExamId == examId && a.OwnerPartnerId == partnerId, ct) ||
                await _context.MinistrySimExamAssignmentsToStudents.AsNoTracking().AnyAsync(a => a.MinistrySimExamId == examId && a.OwnerPartnerId == partnerId, ct);

            if (!hasAssignment)
                return null;

            var exam = await _context.MinistrySimExams.AsNoTracking()
                .Where(e => e.Id == examId)
                .Select(e => new { e.Id, e.Title, CourseName = e.Course.Name })
                .FirstOrDefaultAsync(ct);

            if (exam == null)
                return null;

            var batches = await _context.MinistrySimExamAssignmentsToBatches.AsNoTracking()
                .Where(a => a.MinistrySimExamId == examId && a.OwnerPartnerId == partnerId)
                .OrderBy(a => a.Batch.Name)
                .Select(a => new PartnerMinistrySimBatchVm
                {
                    BatchId = a.BatchId,
                    Name = a.Batch.Name,
                    IsOnline = a.IsOnline,
                    ReferenceCode = a.ReferenceCode,
                    VisibleFrom = a.VisibleFrom
                })
                .ToListAsync(ct);

            if (batchId.HasValue && batches.All(b => b.BatchId != batchId.Value))
                return null;

            var owned = _ownership.PartnerStudents(examId, partnerId);

            if (batchId.HasValue)
            {
                var id = batchId.Value;
                owned = owned.Where(s => _context.StudentBatchEnrollments.Any(e =>
                    e.BatchId == id && e.StudentID == s.StudentID && e.Status == ActiveEnrollmentStatus));
            }

            var students = await owned
                .OrderBy(s => s.FullName)
                .Select(s => new
                {
                    s.StudentID,
                    s.FullName,
                    BatchName = _context.StudentBatchEnrollments
                        .Where(e =>
                            e.StudentID == s.StudentID &&
                            e.Status == ActiveEnrollmentStatus &&
                            _context.MinistrySimExamAssignmentsToBatches.Any(a =>
                                a.MinistrySimExamId == examId && a.OwnerPartnerId == partnerId && a.BatchId == e.BatchId))
                        .Select(e => e.Batch.Name)
                        .FirstOrDefault(),
                    Attempt = _context.MinistrySimExamStudentAttempts
                        .Where(a => a.MinistrySimExamId == examId && a.StudentId == s.StudentID)
                        .Select(a => new { a.IsCompleted, a.TotalScorePercent, a.CompletedAt })
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            return new PartnerMinistrySimStudentsVm
            {
                ExamId = exam.Id,
                Title = exam.Title,
                CourseName = exam.CourseName,
                BatchId = batchId,
                Batches = batches,
                Students = students
                    .Select(s => new PartnerMinistrySimStudentRowVm
                    {
                        StudentId = s.StudentID,
                        FullName = s.FullName,
                        BatchName = s.BatchName,
                        HasAttempt = s.Attempt != null,
                        IsCompleted = s.Attempt?.IsCompleted ?? false,
                        ScorePercent = s.Attempt?.TotalScorePercent,
                        CompletedAt = s.Attempt?.CompletedAt
                    })
                    .ToList()
            };
        }

        public Task<bool> OwnsStudentAsync(int partnerId, int examId, int studentId, CancellationToken ct = default)
            => _ownership.IsPartnerStudentAsync(examId, studentId, partnerId, ct);

        public async Task<bool> SaveParentReportNoteAsync(int partnerId, int attemptId, string? note, CancellationToken ct = default)
        {
            var owned = await _ownership.PartnerAttempts(partnerId).AnyAsync(a => a.Id == attemptId, ct);
            if (!owned)
                return false;

            var value = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            await _context.MinistrySimExamStudentAttempts
                .Where(a => a.Id == attemptId)
                .ExecuteUpdateAsync(u => u.SetProperty(a => a.RecommendationsNote, value), ct);

            return true;
        }

        public async Task<ReportLetterhead> GetLetterheadAsync(int partnerId, CancellationToken ct = default)
        {
            var partner = await _context.Partners.AsNoTracking()
                .Where(p => p.Id == partnerId)
                .Select(p => new { p.Name, p.LogoPath })
                .FirstOrDefaultAsync(ct);

            return new ReportLetterhead
            {
                Name = partner?.Name ?? "",
                LogoUrl = partner?.LogoPath,
                CollaborationNote = CollaborationNote
            };
        }

        public async Task<bool> SetHiddenAsync(int partnerId, int examId, bool hidden, CancellationToken ct = default)
        {
            DateTime? value = hidden ? DateTime.UtcNow : null;

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(ct);

                var batches = await _context.MinistrySimExamAssignmentsToBatches
                    .Where(a => a.MinistrySimExamId == examId && a.OwnerPartnerId == partnerId)
                    .ExecuteUpdateAsync(u => u.SetProperty(a => a.HiddenByOwnerAt, value), ct);

                var students = await _context.MinistrySimExamAssignmentsToStudents
                    .Where(a => a.MinistrySimExamId == examId && a.OwnerPartnerId == partnerId)
                    .ExecuteUpdateAsync(u => u.SetProperty(a => a.HiddenByOwnerAt, value), ct);

                await transaction.CommitAsync(ct);
                return batches + students > 0;
            });
        }
    }
}
