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

    /// <summary>RTK-S4: قراءة ما يراه الطالب + تسجيل تقدّم الفيديو. الانتقالات اللاحقة (الاختبارات) في RTK-S5.</summary>
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
    }
}
