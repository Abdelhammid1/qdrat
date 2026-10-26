using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    public enum RemedialTrackAttemptReviewStatus
    {
        Ok,
        InProgress,     // لم تُغلق بعد ← حوّل لصفحة النتيجة (تتولى الحل/الإغلاق عند انتهاء الوقت)
        ReviewLocked,   // مراجعة الإجابات تُفتح بعد نهاية الاختبار الثاني (أو اجتياز الأول)
        NotFound,       // ليست للطالب، أو محاولة ملحق
        Forbidden       // أمر نشر ملغى/غير منشور بعد
    }

    public sealed record RemedialTrackAttemptReviewResult(RemedialTrackAttemptReviewStatus Status, ExamReviewViewModel? Model = null);

    public sealed record RemedialTrackAttemptReportResult(RemedialTrackAttemptReviewStatus Status, StudentRemedialTrackAttemptReportVm? Model = null);

    /// <summary>
    /// مراجعة الأسئلة والتقرير التفصيلي لمحاولة 101/102 <b>مغلقة</b> فقط (للمالك). تُكشف الإجابات الصحيحة والشرح هنا فقط بعد الإغلاق؛
    /// صفحة الحل (D15) لا تتغير. محاولات الملحق مستبعدة (D32).
    /// </summary>
    public interface IRemedialTrackAttemptReviewService
    {
        Task<RemedialTrackAttemptReviewResult> GetReviewAsync(int studentId, int attemptId, string backUrl, CancellationToken ct = default);

        Task<RemedialTrackAttemptReportResult> GetReportAsync(int studentId, int attemptId, CancellationToken ct = default);
    }
}
