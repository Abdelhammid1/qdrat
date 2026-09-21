namespace QdratNew.Entities
{
    public class InterventionTask
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

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public string DeliveryMode { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        public DateTime? DueDate { get; set; }
        public string OwnerInstructions { get; set; } = string.Empty;
        public string? InstructorExecutionNotes { get; set; }
        public string? TargetLessonsJson { get; set; }
        public string? TargetQuestionsJson { get; set; }
        public string? BeforeSnapshotJson { get; set; }
        public string? AfterSnapshotJson { get; set; }

        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CompletedByUserId { get; set; }
        public DateTime? OwnerReviewedAt { get; set; }
        public string? OwnerReviewNotes { get; set; }
    }
}
