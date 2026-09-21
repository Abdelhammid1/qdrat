namespace QdratNew.Services.Statistics.DTOs
{
    public class HomeworkDraftStatsDto
    {
        public int TotalDrafts { get; set; }

        public double AverageQuestions { get; set; }
        public int ReadyToSendDrafts { get; set; }
        public int HeavyDrafts { get; set; }   // > 30 سؤال
    }
}
