namespace QdratNew.DTOs.Homework
{
    public class SessionStartRequest
    {
        public int HomeworkSetId { get; set; }
        public string? TabId { get; set; }
    }

    public class TimeValidationRequest
    {
        public int HomeworkSetId { get; set; }
        public Guid QuestionId { get; set; }
        public double ClientTimeSecs { get; set; }
    }

    public class TabHeartbeatRequest
    {
        public int HomeworkSetId { get; set; }
        public string TabId { get; set; } = string.Empty;
    }
}
