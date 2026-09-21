namespace QdratNew.Areas.Internal.QuestionBank.ViewModels
{
    public class QuestionBankStatsVM
    {
        public int TotalQuestions { get; set; }
        public int IncompleteCount { get; set; }
        public int MissingAnswerCount { get; set; }
        public int ReadyForReviewCount { get; set; }
        public int ApprovedCount { get; set; }

        public double CompletionRate { get; set; }
        public double ApprovalRate { get; set; }
    }
}
