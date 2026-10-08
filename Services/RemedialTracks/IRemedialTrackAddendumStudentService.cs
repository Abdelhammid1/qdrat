using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S13: مسار الطالب في «ملحق المحور». لا يمرّ إطلاقًا على آلة حالة المحور ولا يغيّر <c>AxisProgress</c> (D29).
    /// الملكية دائمًا عبر <c>EnrollmentId</c> الذي يخص الطالب الحالي من الهوية؛ غير المستهدف/أمر آخر/محور Locked/أمر محذوف أو ملغى/ملحق غير نشط ← null/NotFound.
    /// </summary>
    public interface IRemedialTrackAddendumStudentService
    {
        /// <summary>صفحة الملحق. null = 404. لا يُسلَّم الرابط إلا لمن استُهدف وقبل اكتمال الفيديو.</summary>
        Task<StudentAddendumVm?> GetAsync(int studentId, int enrollmentId, int addendumId, CancellationToken ct = default);

        /// <summary>نبضة فيديو الملحق (D32): نفس الحساب النقي <see cref="RemedialTrackProgressService.ComputePing"/> وتحديث ذرّي بشرط <c>LastPingAtUtc</c>.</summary>
        Task<RemedialTrackPingResult> RecordPingAsync(int studentId, RemedialTrackAddendumPingRequest request, CancellationToken ct = default);

        /// <summary>بدء اختبار الملحق (بعد اكتمال الفيديو فقط، محاولة واحدة جارية في آن واحد، محاولات غير محدودة حتى النجاح).</summary>
        Task<RemedialTrackExamStartResult> StartExamAsync(int studentId, int enrollmentId, int addendumId, CancellationToken ct = default);

        /// <summary>
        /// بعد تسليم/إغلاق محاولة ملحق: يُعيد حساب المحاولات/أفضل نسبة/النجاح من المحاولات المُسلَّمة (Idempotent). لا أثر على AxisProgress.
        /// </summary>
        Task OnAttemptSubmittedAsync(int attemptId, CancellationToken ct = default);
    }
}
