using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Helpers;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.Services.Exams.Models;
using QdratNew.ViewModels.Exam;

namespace QdratNew.Services.MinistrySim
{
    public enum MinistrySimParentReportStatus
    {
        Ok = 0,
        ExamNotFound = 1,
        AttemptNotFound = 2,
        NotCompleted = 3,
        StudentNotFound = 4
    }

    public sealed class MinistrySimParentReportLookup
    {
        public MinistrySimParentReportStatus Status { get; init; }
        public MinistrySimExamParentReportVm? Vm { get; init; }
    }

    // APD-S5.2: بناء تقرير ولي الأمر مستخرج من MinistrySimExamController.ParentReport (Sprint 18/19) بلا تغيير في الحساب،
    // ليخدم الأدمن والمدرسة الشريكة. رأس الخطاب يملؤه المستدعي (إعدادات المعهد أو بيانات المدرسة).
    // التحقق من ملكية الطالب مسؤولية المستدعي قبل الاستدعاء.
    public interface IMinistrySimParentReportService
    {
        // ownerPartnerId: null = المعهد. يحدد متوسط الدفعة المقارن ومحاولات اتجاه الأداء (لا تختلط محاولات الجهتين).
        Task<MinistrySimParentReportLookup> BuildAsync(int examId, int studentId, int? ownerPartnerId, bool print, CancellationToken ct = default);
    }

    public class MinistrySimParentReportService : IMinistrySimParentReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMinistrySimExamResultService _resultService;
        private readonly IMinistrySimExamBatchAnalyticsService _batchAnalyticsService;
        private readonly IMinistrySimOwnershipQuery _ownership;

        public MinistrySimParentReportService(
            ApplicationDbContext context,
            IMinistrySimExamResultService resultService,
            IMinistrySimExamBatchAnalyticsService batchAnalyticsService,
            IMinistrySimOwnershipQuery ownership)
        {
            _context = context;
            _resultService = resultService;
            _batchAnalyticsService = batchAnalyticsService;
            _ownership = ownership;
        }

        public async Task<MinistrySimParentReportLookup> BuildAsync(int examId, int studentId, int? ownerPartnerId, bool print, CancellationToken ct = default)
        {
            var lookup = await _resultService.GetResultAsync(examId, studentId);

            switch (lookup.Status)
            {
                case MinistrySimExamResultStatus.ExamNotFound:
                    return new MinistrySimParentReportLookup { Status = MinistrySimParentReportStatus.ExamNotFound };
                case MinistrySimExamResultStatus.AttemptNotFound:
                    return new MinistrySimParentReportLookup { Status = MinistrySimParentReportStatus.AttemptNotFound };
                case MinistrySimExamResultStatus.NotCompleted:
                    return new MinistrySimParentReportLookup { Status = MinistrySimParentReportStatus.NotCompleted };
            }

            var resultVm = lookup.Vm;

            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.Parent)
                .FirstOrDefaultAsync(s => s.StudentID == studentId, ct);

            if (student == null)
                return new MinistrySimParentReportLookup { Status = MinistrySimParentReportStatus.StudentNotFound };

            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.Id == examId, ct);

            if (exam == null)
                return new MinistrySimParentReportLookup { Status = MinistrySimParentReportStatus.ExamNotFound };

            // بطاقة المقارنة: دفعة الطالب الفعلية ضمن دورة هذا الاختبار (بصرف النظر عن مسار الإسناد — دفعة أو فردي)
            // APD-S5.2: دفعة من جهة المالك نفسها (المعهد أو المدرسة)
            var studentBatch = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.StudentID == studentId && e.Status == "Active" && e.Batch.CourseId == exam.CourseId
                            && e.Batch.Branch.PartnerId == ownerPartnerId)
                .Select(e => new { e.BatchId, BatchName = e.Batch.Name })
                .FirstOrDefaultAsync(ct);

            var batchStats = studentBatch != null
                ? await _batchAnalyticsService.GetBatchAverageStatsAsync(examId, studentBatch.BatchId, ownerPartnerId)
                : new MinistrySimExamBatchAverageStatsVm();

            // المحاولة الحالية — نحتاج Id لحفظ/عرض RecommendationsNote (Sprint 19 / K3)
            var attempt = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .Where(a => a.MinistrySimExamId == examId && a.StudentId == studentId)
                .Select(a => new { a.Id, a.RecommendationsNote })
                .FirstOrDefaultAsync(ct);

            // اتجاه الأداء عبر كل محاولات هذا الطالب المكتملة في اختبارات محاكاة سابقة (Sprint 19 / K3)
            // APD-S5.2: محاولات نفس الجهة فقط، فلا تظهر للمدرسة اختبارات المعهد ولا العكس
            var ownedAttempts = ownerPartnerId.HasValue
                ? _ownership.PartnerAttempts(ownerPartnerId.Value)
                : _ownership.PlatformAttempts();

            var trend = await ownedAttempts
                .Where(a => a.StudentId == studentId && a.IsCompleted && a.CompletedAt != null)
                .OrderBy(a => a.CompletedAt)
                .Select(a => new MinistrySimExamParentReportTrendPointVm
                {
                    ExamTitle = a.MinistrySimExam.Title,
                    CompletedAt = a.CompletedAt.Value,
                    TotalScorePercent = a.TotalScorePercent ?? 0
                })
                .ToListAsync(ct);

            var vm = new MinistrySimExamParentReportVm
            {
                MinistrySimExamId = exam.Id,
                ExamTitle = exam.Title,
                CourseName = exam.Course.Name,

                StudentId = student.StudentID,
                StudentFullName = student.FullName,
                BatchName = studentBatch?.BatchName,

                ParentFullName = student.Parent?.FullName,
                ParentPhoneNumber = student.Parent?.PhoneNumber,
                ParentRelationToStudent = student.Parent?.RelationToStudent.ToString(),

                AttemptId = attempt?.Id ?? 0,
                AttemptCompletedAt = resultVm.CompletedAt,
                TotalScorePercent = resultVm.TotalScorePercent,
                QuantScorePercent = resultVm.QuantPercent,
                VerbalScorePercent = resultVm.VerbalPercent,
                Stages = resultVm.Stages.Select(s => new MinistrySimExamParentReportStageVm
                {
                    StageNumber = s.StageNumber,
                    StagePercentScore = s.StagePercentScore,
                    TimeExpired = s.TimeExpired,
                    DurationMinutes = s.DurationMinutes,
                    MotivationMessage = ResultToneHelper.Build(s.StagePercentScore, ResultContext.Exam).MotivationMsg
                }).ToList(),

                BatchTotalStudents = batchStats.TotalStudents,
                BatchAvgScorePercent = batchStats.AvgScorePercent,
                BatchAvgQuantPercent = batchStats.AvgQuantPercent,
                BatchAvgVerbalPercent = batchStats.AvgVerbalPercent,

                PreviousAttemptsTrend = trend,
                RecommendationsNote = attempt?.RecommendationsNote,
                IsPrintMode = print
            };

            // توصية محفِّزة عامة على مستوى الاختبار كامل — نفس نبرة "بلا لغة رسوب" المعتمدة بكل المنصة
            var overallTone = ResultToneHelper.Build(vm.TotalScorePercent, ResultContext.Exam);
            vm.OverallMotivationHeading = overallTone.MotivationHeading;
            vm.OverallMotivationMessage = overallTone.MotivationMsg;

            return new MinistrySimParentReportLookup { Status = MinistrySimParentReportStatus.Ok, Vm = vm };
        }
    }
}
