namespace QdratNew.Modules.QuestionBank.Insights.Dtos
{
    public class QuestionCoverageInsightDto
    {
        public string Curriculum { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int ApprovedQuestions { get; set; }
    }
}
