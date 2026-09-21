namespace QdratNew.Modules.QuestionBank.Read.Dtos
{
    public class QuestionBankStatsDto
    {
        public int TotalQuestions { get; set; }

        public int IncompleteCount { get; set; }
        public int MissingAnswerCount { get; set; }
        public int ReadyForReviewCount { get; set; }
        public int ApprovedCount { get; set; }

        public double CompletionRate { get; set; }   // %
        public double ApprovalRate { get; set; }     // %

        public DateTime GeneratedAt { get; set; }
    }
}
