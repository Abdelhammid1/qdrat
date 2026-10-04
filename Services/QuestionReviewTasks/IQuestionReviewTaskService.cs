using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.ViewModels.QuestionReviewTasks;

namespace QdratNew.Services.QuestionReviewTasks
{
    public sealed record ReviewActor(string UserId, string Name, UserRoleType Role);

    public sealed record OperationResult(bool Success, string Message, object? Data = null)
    {
        public static OperationResult Ok(string m, object? d = null) => new(true, m, d);
        public static OperationResult Fail(string m) => new(false, m);
    }

    /// <summary>مدرب مؤهل لاستلام مهمة مراجعة، مع عبء العمل الحالي (أسئلة محجوزة بانتظار مراجعته).</summary>
    public sealed record EligibleInstructorDto(int Id, string FullName, int ActiveLockedItems);

    /// <summary>قرار الأدمن على سؤال خارج المهمة، يُزامَن مع عناصر المهام (QRT-S4.3).</summary>
    public enum AdminQuestionDecision { Approved = 1, Rejected = 2, Unapproved = 3 }

    public enum ReturnResolution { ApproveAsIs = 1, ReleaseToPool = 2, Reject = 3 }  // EditAndApprove يتم عبر شاشة التعديل ثم ApproveAsIs

    public interface IQuestionReviewTaskService
    {
        // الأدمن
        Task<OperationResult> CreateTaskAsync(CreateQuestionReviewTaskInput input, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> CancelTaskAsync(int taskId, string reason, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> CloseTaskAsync(int taskId, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> ExtendDueAsync(int taskId, DateTime? newDueUtc, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> RemoveItemsAsync(int taskId, IReadOnlyCollection<long> itemIds, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> ReassignRemainingAsync(int taskId, int newInstructorId, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> ResolveReturnedAsync(long itemId, ReturnResolution resolution, string? note, ReviewActor actor, CancellationToken ct = default);

        /// <summary>
        /// المدربون المؤهلون للأسئلة المحددة (أو لأول N حسب الفلتر): نشط + له حساب دخول + يملك كل المناهج المعنية + نفس الشريك.
        /// Data = IReadOnlyList&lt;EligibleInstructorDto&gt; مرتبة حسب العبء ثم الاسم.
        /// </summary>
        Task<OperationResult> GetEligibleInstructorsAsync(EligibleInstructorsInput input, CancellationToken ct = default);

        /// <summary>
        /// QRT-S6.2: المدربون المؤهلون لاستلام المتبقي (المعلّق) من مهمة، باستثناء مدربها الحالي.
        /// Data = { pendingCount, instructors: IReadOnlyList&lt;EligibleInstructorDto&gt; }.
        /// </summary>
        Task<OperationResult> GetReassignCandidatesAsync(int taskId, CancellationToken ct = default);

        /// <summary>
        /// QRT-S4.3: مزامنة عناصر المهام مع حالة الأسئلة بعد اعتماد/رفض/إلغاء اعتماد من الأدمن (تُستدعى بعد حفظ تغيير السؤال).
        /// مبنية على حالة السؤال الفعلية لا على قائمة معرّفات، فتُصلح أي عنصر فاته التحديث سابقًا.
        /// لا ترمي استثناءً أبدًا: فشلها يُسجَّل فقط ولا يُبطل قرار الأدمن. تُرجع عدد العناصر المحدَّثة.
        /// </summary>
        Task<int> SyncAdminDecisionAsync(AdminQuestionDecision decision, ReviewActor actor, CancellationToken ct = default);

        // المدرب (instructorId من الخادم دائمًا — D6)
        Task<OperationResult> ApproveItemsAsync(int instructorId, int taskId, IReadOnlyCollection<long> itemIds, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> MarkEditedAndApprovedAsync(int instructorId, long itemId, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> ReturnItemAsync(int instructorId, long itemId, string note, ReviewActor actor, CancellationToken ct = default);

        Task RecalculateCountersAsync(ApplicationDbContext db, int taskId, CancellationToken ct = default);
    }
}
