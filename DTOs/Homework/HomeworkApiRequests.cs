namespace QdratNew.DTOs.Homework
{
    public class SaveAnswerRequest
    {
        public int HomeworkSetId { get; set; }
        public Guid QuestionId { get; set; }
        public string? SelectedAnswer { get; set; }
        public double TimeTakenSeconds { get; set; }
        public bool IsMarkedForReview { get; set; }
    }

    public class ReviewNavigationEntry
    {
        public Guid QuestionId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SaveReviewSessionRequest
    {
        public int HomeworkSetId { get; set; }
        public DateTime EnteredAt { get; set; }
        public List<ReviewNavigationEntry> NavigationLog { get; set; } = new();
    }

    public class FinalSubmitRequest
    {
        public int HomeworkSetId { get; set; }
        public double TotalTimeSeconds { get; set; }
    }
}
