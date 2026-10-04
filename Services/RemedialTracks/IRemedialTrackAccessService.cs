using QdratNew.Enums;

namespace QdratNew.Services.RemedialTracks
{
    public enum RemedialTrackAccessOutcome
    {
        Allowed,
        NotFound,
        NotYetPublished,
        Cancelled,
        NeedsCode,
        CodeLocked
    }

    /// <summary>لقطة خفيفة تكفي لقرار البوابة (تُستعمل أيضًا داخل استعلام نبضة الفيديو دون استعلام إضافي).</summary>
    public readonly record struct RemedialTrackAccessSnapshot(
        int StudentId,
        RemedialTrackEnrollmentStatus EnrollmentStatus,
        RemedialTrackPublicationStatus PublicationStatus,
        DateTime PublishAtUtc,
        RemedialTrackDeliveryMode Mode,
        int CodeVersion,
        int? VerifiedCodeVersion,
        DateTime? CodeLockedUntilUtc);

    public sealed record RemedialTrackAccessResult(
        RemedialTrackAccessOutcome Outcome,
        DateTime? LockedUntilUtc = null,
        string? TrackTitle = null)
    {
        public bool IsAllowed => Outcome == RemedialTrackAccessOutcome.Allowed;
    }

    public enum RemedialTrackVerifyOutcome
    {
        Verified,
        Invalid,        // صيغة خاطئة أو رقم خاطئ — رسالة عامة دائمًا
        Locked,
        NotFound,
        NotYetPublished,
        Cancelled
    }

    public sealed record RemedialTrackVerifyResult(
        RemedialTrackVerifyOutcome Outcome,
        DateTime? LockedUntilUtc = null)
    {
        public bool IsVerified => Outcome == RemedialTrackVerifyOutcome.Verified;
    }

    /// <summary>RTK-S4.2: بوابة الوصول والرقم المرجعي (D6/D7/D8/D14).</summary>
    public interface IRemedialTrackAccessService
    {
        /// <summary>يُستدعى في كل Action طالب. يسجّل بداية التسجيل عند أول وصول مسموح.</summary>
        Task<RemedialTrackAccessResult> EvaluateAsync(int studentId, int enrollmentId, CancellationToken ct = default);

        /// <summary>التحقق من الرقم المرجعي (حضوري). يخزّن النتيجة في قاعدة البيانات لا في Session.</summary>
        Task<RemedialTrackVerifyResult> VerifyCodeAsync(int studentId, int enrollmentId, string? code, CancellationToken ct = default);

        /// <summary>هل لدى الطالب تسجيل نشط ظاهر له (للقائمة الجانبية)؟</summary>
        Task<bool> HasVisibleEnrollmentAsync(int studentId, CancellationToken ct = default);
    }
}
