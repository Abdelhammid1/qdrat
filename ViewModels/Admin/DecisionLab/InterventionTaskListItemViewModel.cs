namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class InterventionTaskListViewModel
    {
        public int TotalTasks { get; set; }
        public int AssignedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int CompletedByInstructorTasks { get; set; }
        public int OverdueTasks { get; set; }
        public IReadOnlyList<InterventionTaskListItemViewModel> Tasks { get; set; } =
            new List<InterventionTaskListItemViewModel>();
    }

    public class InterventionTaskListItemViewModel
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public int? CurriculumId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public string LectureTitle { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public string DeliveryMode { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsOverdue { get; set; }
    }
}
