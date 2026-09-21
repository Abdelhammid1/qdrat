namespace QdratNew.DTOs.Homework
{
    public class HomeworkQuestionAttemptInput
    {
        public int StudentId { get; set; }
        public int HomeworkSetId { get; set; }
        public int HomeworkSetAttemptId { get; set; }

        public Guid QuestionId { get; set; }

        public string SelectedAnswer { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }

        // ⏱️ اختياري – ليس أولوية الآن
        public double TimeTakenSeconds { get; set; }

        public bool IsMarkedForReview { get; set; }
    }
}
