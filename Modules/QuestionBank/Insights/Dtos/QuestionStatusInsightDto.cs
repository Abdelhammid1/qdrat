namespace QdratNew.Modules.QuestionBank.Insights.Dtos
{
    public class QuestionStatusInsightDto
    {
        public int Approved { get; set; }
        public int ReadyForReview { get; set; }
        public int MissingAnswer { get; set; }
        public int Incomplete { get; set; }

        public int Total =>
            Approved + ReadyForReview + MissingAnswer + Incomplete;
    }
}
