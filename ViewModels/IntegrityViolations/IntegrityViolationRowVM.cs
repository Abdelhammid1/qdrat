using QdratNew.Enums;

namespace QdratNew.ViewModels.IntegrityViolations
{
    public class IntegrityViolationRowVM
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public string? StudentName { get; set; }

        public IntegrityAttemptType AttemptType { get; set; }
        public int AttemptEntityId { get; set; }

        public string ViolationType { get; set; } = string.Empty;

        public DateTime DetectedAt { get; set; }
        public string? IpAddress { get; set; }
        public string? PageUrl { get; set; }

        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolutionNote { get; set; }
        public bool SelfResolved { get; set; }
    }
}
