namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class InterventionTaskDetailsViewModel
    {
        public int Id { get; set; }
        public int? DecisionRecommendationId { get; set; }
        public int BatchId { get; set; }
        public int? CourseId { get; set; }
        public int? CurriculumId { get; set; }
        public int? SectionId { get; set; }
        public int? LessonId { get; set; }
        public int? InstructorId { get; set; }
        public int? LectureId { get; set; }

        public string BatchName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string CurriculumTitle { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public string LectureTitle { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public string DeliveryMode { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public string OwnerInstructions { get; set; } = string.Empty;
        public string? TargetLessonsJson { get; set; }
        public string? TargetQuestionsJson { get; set; }

        public string RecommendationSummary { get; set; } = string.Empty;
        public string RecommendationReason { get; set; } = string.Empty;
        public string RecommendationType { get; set; } = string.Empty;
        public string RecommendationStatus { get; set; } = string.Empty;

        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public SnapshotComparisonVM? SnapshotComparison { get; set; }
        public string? AfterSnapshotJson { get; set; }
    }
}
