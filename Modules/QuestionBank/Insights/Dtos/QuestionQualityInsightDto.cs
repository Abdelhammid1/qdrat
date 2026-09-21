namespace QdratNew.Modules.QuestionBank.Insights.Dtos
{
    public class QuestionQualityInsightDto
    {
        public int WithInternalNotes { get; set; }
        public int WithoutInternalNotes { get; set; }

        public int Reviewed { get; set; }
        public int NotReviewed { get; set; }
    }
}
