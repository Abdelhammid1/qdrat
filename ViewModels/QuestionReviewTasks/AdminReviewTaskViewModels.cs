using QdratNew.Enums;

namespace QdratNew.ViewModels.QuestionReviewTasks
{
    /// <summary>فلاتر قائمة المهام عند الأدمن (QRT-S5.1). كلها اختيارية.</summary>
    public sealed class AdminTasksFilter
    {
        public QuestionReviewTaskStatus? Status { get; init; }
        public int? InstructorId { get; init; }
        public int? CurriculumId { get; init; }
        public QuestionReviewTaskPriority? Priority { get; init; }
        public bool OverdueOnly { get; init; }
        public string? Search { get; init; }
    }

    /// <summary>مؤشرات أعلى الصفحة — كلها من العدادات المخزّنة على المهام.</summary>
    public sealed class AdminTasksKpisVm
    {
        public int OpenTasks { get; init; }
        public int LockedPending { get; init; }
        public int OverdueTasks { get; init; }
        public int UnresolvedReturns { get; init; }
        public int AverageApprovalPercent { get; init; }
    }

    public sealed class AdminInstructorApprovalVm
    {
        public int InstructorId { get; init; }
        public string InstructorName { get; init; } = string.Empty;
        public int Tasks { get; init; }
        public int Pending { get; init; }
        public int Approved { get; init; }
        public int Returned { get; init; }
        public int Effective { get; init; }
        public int ApprovalPercent { get; init; }
    }

    public sealed class AdminFilterOptionVm
    {
        public int Id { get; init; }
        public string Text { get; init; } = string.Empty;
    }

    /// <summary>نموذج صفحة Index: المؤشرات وخيارات الفلاتر وملخص المدربين (الجدول نفسه يُحمَّل خادميًا).</summary>
    public sealed class AdminTasksIndexVm
    {
        public AdminTasksKpisVm Kpis { get; init; } = new();
        public List<AdminFilterOptionVm> Instructors { get; init; } = new();
        public List<AdminFilterOptionVm> Curriculums { get; init; } = new();
        public List<AdminInstructorApprovalVm> ByInstructor { get; init; } = new();
    }

    /// <summary>صف جدول المهام (DataTables خادمي).</summary>
    public sealed class AdminTaskRowDto
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string InstructorName { get; init; } = "—";
        public string? CurriculumTitle { get; init; }
        public int Status { get; init; }
        public string StatusLabel { get; init; } = string.Empty;
        public int Priority { get; init; }
        public string? DueLocal { get; init; }
        public bool IsOverdue { get; init; }
        public int Total { get; init; }
        public int Effective { get; init; }
        public int Pending { get; init; }
        public int Approved { get; init; }
        public int Returned { get; init; }
        public int ApprovalPercent { get; init; }
        public int HandledPercent { get; init; }
    }

    public sealed class AdminTasksPage
    {
        public int Total { get; init; }
        public int Filtered { get; init; }
        public List<AdminTaskRowDto> Rows { get; init; } = new();
    }

    public sealed class AdminTimelineEventVm
    {
        public DateTime AtUtc { get; init; }
        public string AtLocal { get; init; } = string.Empty;
        public string Icon { get; init; } = "fa-circle";
        public string Tone { get; init; } = "brand";
        public string Text { get; init; } = string.Empty;
        public string? Detail { get; init; }
    }

    public sealed class AdminTaskDetailsVm
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? AdminNote { get; init; }
        public string InstructorName { get; init; } = "—";
        public string? CurriculumTitle { get; init; }
        public string? ParentTaskCode { get; init; }
        public string? CreatedByName { get; init; }
        public QuestionReviewTaskStatus Status { get; init; }
        public QuestionReviewTaskPriority Priority { get; init; }
        public string? DueLocal { get; init; }
        public string? CreatedLocal { get; init; }
        public string? CancelReason { get; init; }
        public bool IsOverdue { get; init; }
        public ReviewTaskProgressVm Progress { get; init; } = new();
        public List<AdminTimelineEventVm> Timeline { get; init; } = new();
    }

    public sealed class AdminItemRowDto
    {
        public long ItemId { get; init; }
        public Guid QuestionId { get; init; }
        public string Reference { get; init; } = "—";
        public string Title { get; init; } = string.Empty;
        public string Section { get; init; } = "—";
        public string Lesson { get; init; } = "—";
        public int Status { get; init; }
        public string StatusLabel { get; init; } = string.Empty;
        public string? ActionBy { get; init; }
        public string? ActionLocal { get; init; }
        public string? ReturnNote { get; init; }
        public string? AdminResolutionNote { get; init; }
    }

    public sealed class AdminItemsPage
    {
        public int Total { get; init; }
        public int Filtered { get; init; }
        public List<AdminItemRowDto> Rows { get; init; } = new();
    }

    /// <summary>بطاقة داشبورد بنك الأسئلة (QRT-S5.3).</summary>
    public sealed class ReviewTasksSummaryVm
    {
        public int OpenTasks { get; init; }
        public int LockedPending { get; init; }
        public int Overdue { get; init; }
        public int ApprovalPercent { get; init; }
    }
}
