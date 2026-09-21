namespace QdratNew.Entities
{
    public class DecisionRecommendation
    {
        public int Id { get; set; }

        public string SourceType { get; set; } = string.Empty;
        public string EngineType { get; set; } = string.Empty;
        public string RecommendationType { get; set; } = string.Empty;

        public int? BatchId { get; set; }
        public int? CurriculumId { get; set; }

        public string InputSnapshotJson { get; set; } = string.Empty;
        public string RecommendationJson { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        public string RequestedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public string? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int? CreatedExamAssignmentId { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
