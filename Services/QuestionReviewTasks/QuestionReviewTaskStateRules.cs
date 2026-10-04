using QdratNew.Enums;

namespace QdratNew.Services.QuestionReviewTasks
{
    /// <summary>
    /// QRT-S6: آلة حالة المهمة في مكان واحد — تستخدمها الخدمة (للرفض في الخادم) والواجهة (لإظهار الأزرار).
    /// Assigned → InProgress → Completed → Closed، والإلغاء من أي حالة نشطة/مكتملة؛ Closed/Cancelled نهائيتان.
    /// </summary>
    public static class QuestionReviewTaskStateRules
    {
        public static bool IsFinal(QuestionReviewTaskStatus status)
            => status == QuestionReviewTaskStatus.Closed || status == QuestionReviewTaskStatus.Cancelled;

        /// <summary>إلغاء المهمة: من Assigned/InProgress/Completed فقط.</summary>
        public static bool CanCancel(QuestionReviewTaskStatus status) => !IsFinal(status);

        /// <summary>الإغلاق: من Completed فقط وبشرط ألا يبقى مرتجع غير معالج.</summary>
        public static bool CanClose(QuestionReviewTaskStatus status, int returnedItems)
            => status == QuestionReviewTaskStatus.Completed && returnedItems == 0;

        /// <summary>تمديد الموعد/إزالة العناصر/إعادة الإسناد: للمهام النشطة فقط (لا يوجد معلّق في غيرها).</summary>
        public static bool CanExtendDue(QuestionReviewTaskStatus status) => QuestionReviewTaskMetrics.IsActive(status);

        public static bool CanRemoveItems(QuestionReviewTaskStatus status, int pendingItems)
            => QuestionReviewTaskMetrics.IsActive(status) && pendingItems > 0;

        public static bool CanReassign(QuestionReviewTaskStatus status, int pendingItems)
            => QuestionReviewTaskMetrics.IsActive(status) && pendingItems > 0;

        /// <summary>
        /// معالجة المرتجع مسموحة في أي حالة: المرتجع يبقى محجوزًا عن المسار العام (S4.2)
        /// حتى بعد إلغاء المهمة، ولا مخرج له إلا بقرار الأدمن.
        /// </summary>
        public static bool CanResolveReturned(QuestionReviewTaskStatus status) => true;
    }
}
