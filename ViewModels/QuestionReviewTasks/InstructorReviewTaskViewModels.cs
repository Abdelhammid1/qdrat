using QdratNew.Enums;

namespace QdratNew.ViewModels.QuestionReviewTasks
{
    /// <summary>ملخص تقدّم مهمة (يُستخدم في بطاقات «مهامي» ورأس صفحة المراجعة وردود AJAX).</summary>
    public sealed class ReviewTaskProgressVm
    {
        public int Total { get; init; }
        public int Pending { get; init; }
        public int Approved { get; init; }
        public int Returned { get; init; }
        public int Removed { get; init; }
        public int Effective { get; init; }
        public int ApprovalPercent { get; init; }
        public int HandledPercent { get; init; }
    }

    public sealed class InstructorTaskCardVm
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? CurriculumTitle { get; init; }
        public QuestionReviewTaskStatus Status { get; init; }
        public QuestionReviewTaskPriority Priority { get; init; }
        public string? DueLocal { get; init; }
        public bool IsOverdue { get; init; }
        public string? AssignedLocal { get; init; }
        public ReviewTaskProgressVm Progress { get; init; } = new();
    }

    public sealed class InstructorTasksIndexVm
    {
        public QuestionReviewTaskStatus? StatusFilter { get; init; }
        public int ActiveCount { get; init; }
        public int PendingQuestions { get; init; }
        public int OverdueCount { get; init; }
        public int CompletedCount { get; init; }
        public List<InstructorTaskCardVm> Tasks { get; init; } = new();
    }

    public sealed class InstructorTaskReviewVm
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? AdminNote { get; init; }
        public string? CurriculumTitle { get; init; }
        public QuestionReviewTaskStatus Status { get; init; }
        public QuestionReviewTaskPriority Priority { get; init; }
        public string? DueLocal { get; init; }
        public bool IsOverdue { get; init; }
        public bool CanAct { get; init; }
        public ReviewTaskProgressVm Progress { get; init; } = new();
    }

    /// <summary>صف عنصر في جدول المراجعة (DataTables خادمي).</summary>
    public sealed class ReviewItemRowDto
    {
        public long ItemId { get; init; }
        public Guid QuestionId { get; init; }
        public string Reference { get; init; } = "—";
        public string Title { get; init; } = string.Empty;
        public string Section { get; init; } = "—";
        public string Lesson { get; init; } = "—";
        public int Status { get; init; }
        public string StatusLabel { get; init; } = string.Empty;
        public string? ReturnNote { get; init; }
        public bool CanAct { get; init; }
    }

    public sealed class ReviewItemsPage
    {
        public int Total { get; init; }
        public int Filtered { get; init; }
        public List<ReviewItemRowDto> Rows { get; init; } = new();
        public ReviewTaskProgressVm Progress { get; init; } = new();
        public QuestionReviewTaskStatus TaskStatus { get; init; }
    }
}
