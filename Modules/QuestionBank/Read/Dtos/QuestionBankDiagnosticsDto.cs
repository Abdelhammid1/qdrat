namespace QdratNew.Modules.QuestionBank.Read.Dtos
{
    public class QuestionBankDiagnosticsDto
    {
        public int TotalQuestions { get; set; }

        public int IncompleteCount { get; set; }
        public int MissingAnswerCount { get; set; }
        public int ReadyForReviewCount { get; set; }
        public int ApprovedCount { get; set; }

        public double QueryExecutionMilliseconds { get; set; }

        public int PageSize { get; set; }
        public int ReturnedItems { get; set; }

        public DateTime GeneratedAt { get; set; }
    }
}
