using QdratNew.Enums;

namespace QdratNew.Services.Interfaces
{
    public interface IIntegrityGuardService
    {
        Task<bool> IsBlockedAsync(int studentId, IntegrityAttemptType attemptType, int attemptEntityId);

        Task LogViolationAsync(
            int studentId,
            IntegrityAttemptType attemptType,
            int attemptEntityId,
            string violationType,
            string? ipAddress,
            string? userAgent,
            string? pageUrl);

        Task<bool> ResolveAsync(int violationLogId, string adminUserId, string? note);

        /// <summary>يحدد إن كان الطالب لا يزال يملك محاولة العودة الذاتية (لم يستخدمها من قبل) لهذه المحاولة تحديدًا.</summary>
        Task<bool> CanSelfResolveAsync(int studentId, IntegrityAttemptType attemptType, int attemptEntityId);

        /// <summary>يحاول إعادة فتح المحاولة النشطة ذاتيًا من الطالب — مسموح مرة واحدة فقط لكل (طالب/نوع/كيان).</summary>
        Task<(bool Success, string Message)> TrySelfResolveAsync(int studentId, IntegrityAttemptType attemptType, int attemptEntityId);

        /// <summary>يعيد فتح المحاولة النشطة (إن وُجدت) لطالب معيّن من الأدمن دون الحاجة لمعرفة رقم سجل المخالفة مسبقًا.</summary>
        Task<bool> ResolveActiveAsync(int studentId, IntegrityAttemptType attemptType, int attemptEntityId, string adminUserId, string? note);
    }
}
