using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.QuestionReviewTasks;

namespace QdratNew.Services.QuestionReviewTasks
{
    /// <summary>QRT-S4.1: عنصر معلّق يحق لمدربه تعديله ضمن مهمته.</summary>
    public sealed record EditableTaskItem(long ItemId, Guid QuestionId, int TaskId, string TaskCode);

    /// <summary>
    /// قراءات مهام المراجعة لجانب المدرب (QRT-S3). كل دالة تتحقق من ملكية المهمة في الاستعلام نفسه (D6).
    /// </summary>
    public interface IQuestionReviewTaskQueryService
    {
        Task<InstructorTasksIndexVm> GetInstructorTasksAsync(int instructorId, QuestionReviewTaskStatus? status, CancellationToken ct = default);

        /// <summary>null إن لم تكن المهمة تخص المدرب.</summary>
        Task<InstructorTaskReviewVm?> GetInstructorTaskReviewAsync(int instructorId, int taskId, CancellationToken ct = default);

        /// <summary>صفحة عناصر (≤100) لمهمة المدرب؛ null إن لم تكن مهمته. statusFilter: قيمة QuestionReviewTaskItemStatus أو null للكل.</summary>
        Task<ReviewItemsPage?> GetItemsPageAsync(
            int instructorId, int taskId, int start, int length, int? statusFilter, string? search, CancellationToken ct = default);

        /// <summary>السؤال (بخياراته) لمعاينة عنصر يخص مهمة المدرب؛ null إن لم يكن له.</summary>
        Task<Question?> GetPreviewQuestionAsync(int instructorId, long itemId, CancellationToken ct = default);

        /// <summary>
        /// QRT-S4.1: العنصر إن كان معلّقًا ومحجوزًا وضمن مهمة نشطة تخص المدرب (الملكية في الاستعلام نفسه)؛ وإلا null.
        /// </summary>
        Task<EditableTaskItem?> GetEditableItemAsync(int instructorId, long itemId, CancellationToken ct = default);

        /// <summary>عدد الأسئلة المعلّقة في مهام المدرب النشطة (للشارة في القائمة الجانبية) — مخزَّن مؤقتًا 60 ثانية.</summary>
        Task<int> GetPendingCountByUserIdAsync(string userId, CancellationToken ct = default);

        void InvalidatePendingCount(string userId);
    }
}
