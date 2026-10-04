using QdratNew.ViewModels.QuestionReviewTasks;

namespace QdratNew.Services.QuestionReviewTasks
{
    /// <summary>
    /// قراءات متابعة الأدمن لمهام المراجعة (QRT-S5). قراءة فقط؛ كل الأرقام من العدادات المخزّنة (D10)
    /// ولا يوجد تجميع على جدول العناصر في قوائم المهام.
    /// </summary>
    public interface IQuestionReviewTaskAdminQueryService
    {
        /// <summary>المؤشرات + خيارات الفلاتر + نسبة الاعتماد لكل مدرب.</summary>
        Task<AdminTasksIndexVm> GetIndexAsync(CancellationToken ct = default);

        /// <summary>صفحة مهام (≤100) بترتيب خادمي من قائمة بيضاء.</summary>
        Task<AdminTasksPage> GetTasksPageAsync(
            AdminTasksFilter filter, int start, int length, int orderColumn, bool orderDescending, CancellationToken ct = default);

        /// <summary>null إن لم توجد المهمة.</summary>
        Task<AdminTaskDetailsVm?> GetDetailsAsync(int taskId, CancellationToken ct = default);

        /// <summary>صفحة عناصر (≤100) لمهمة؛ null إن لم توجد المهمة. statusFilter: قيمة QuestionReviewTaskItemStatus أو null للكل.</summary>
        Task<AdminItemsPage?> GetItemsPageAsync(
            int taskId, int start, int length, int? statusFilter, string? search, CancellationToken ct = default);

        /// <summary>ملخص بطاقة داشبورد بنك الأسئلة (المهام المفتوحة + نسبة الاعتماد لآخر 90 يومًا).</summary>
        Task<ReviewTasksSummaryVm> GetDashboardSummaryAsync(CancellationToken ct = default);

        /// <summary>QRT-S7.3: أداء المراجعين في فترة (استعلام مهام + استعلام عناصر مجمّع، بلا N+1).</summary>
        Task<ReviewersReportVm> GetReviewersReportAsync(ReviewersReportFilter filter, CancellationToken ct = default);

        /// <summary>QRT-S7.2: بيانات صفحة التوزيع التلقائي (قائمة المناهج).</summary>
        Task<AutoDistributePageVm> GetAutoDistributePageAsync(CancellationToken ct = default);
    }
}
