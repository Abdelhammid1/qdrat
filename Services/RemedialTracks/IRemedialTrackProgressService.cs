using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    public enum RemedialTrackPingStatus
    {
        Ok,
        BadRequest,     // 400: state غير مقبولة
        NotFound,       // 404: الفيديو لا يخص هذا الطالب/التسجيل
        Conflict,       // 409: الحالة لا تسمح أو فيديو سابق غير مكتمل
        NeedsCode,      // 403: يلزم الرقم المرجعي (حضوري)
        Forbidden       // 403: أمر نشر ملغى/غير منشور بعد/مقفول
    }

    public sealed record RemedialTrackPingResult(RemedialTrackPingStatus Status, RemedialTrackVideoPingResponse Body);

    /// <summary>
    /// نتيجة انتقال حالة. Applied=false بلا Error ← الانتقال مُنفَّذ سابقًا (Idempotent) والحالة الحالية في Status.
    /// Error ≠ null ← الانتقال غير مسموح من الحالة الحالية.
    /// </summary>
    public sealed record RemedialTrackTransitionResult(
        bool Applied,
        QdratNew.Enums.RemedialTrackAxisStatus? Status,
        int Round,
        string? Error = null);

    /// <summary>RTK-S4: قراءة ما يراه الطالب + تسجيل تقدّم الفيديو. RTK-S5: الانتقالات فوق آلة الحالة.</summary>
    public interface IRemedialTrackProgressService
    {
        /// <summary>RTK-S4.1: أوامر نشر الطالب الظاهرة (نشطة + وقت النشر حلّ + غير ملغاة).</summary>
        Task<StudentRemedialTrackIndexVm> GetMyPlansAsync(int studentId, CancellationToken ct = default);

        /// <summary>RTK-S4.3: صفحة الخطة (المحاور مرتبة بالحالة). null = غير موجود لهذا الطالب. تفترض اجتياز بوابة الوصول.</summary>
        Task<StudentRemedialTrackPlanVm?> GetPlanAsync(int studentId, int enrollmentId, CancellationToken ct = default);

        /// <summary>RTK-S4.3: صفحة المحور. تُنشئ صفوف تقدّم الفيديو للجولة كسولًا. null = غير موجود/مغلق.</summary>
        Task<StudentRemedialTrackAxisVm?> GetAxisAsync(int studentId, int enrollmentId, int axisProgressId, CancellationToken ct = default);

        /// <summary>RTK-S4.4: نبضة الفيديو — الخادم هو المرجع الوحيد للإتمام وفتح التالي (D4).</summary>
        Task<RemedialTrackPingResult> RecordPingAsync(int studentId, RemedialTrackVideoPingRequest request, CancellationToken ct = default);

        /// <summary>RTK-S5.1: بعد اكتمال فيديوهات الجولة ← AwaitingExam101/102. Idempotent.</summary>
        Task<RemedialTrackTransitionResult> OnAllVideosCompletedAsync(int axisProgressId, CancellationToken ct = default);

        /// <summary>
        /// RTK-S5.1: يقرأ نتيجة المحاولة المُسلَّمة وينفّذ الانتقال (اجتياز/جولة 2/حجب + فتح التالي + حالة التسجيل + الأحداث)
        /// داخل Transaction + RowVersion. Idempotent: التسليم المزدوج لا أثر له.
        /// </summary>
        Task<RemedialTrackTransitionResult> OnExamSubmittedAsync(int attemptId, CancellationToken ct = default);

        /// <summary>RTK-S5.1 (يُستدعى من واجهة الأدمن في S6): فتح المحور التالي لمحور حُجب. السبب إلزامي (≤ 300). Idempotent.</summary>
        Task<RemedialTrackTransitionResult> AdminOpenNextAsync(int axisProgressId, string? reason, RemedialTrackActor actor, CancellationToken ct = default);
    }
}
