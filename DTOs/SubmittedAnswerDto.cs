namespace QdratNew.DTOs
{
    public class SubmittedAnswerDto
    {
        public int HomeworkId { get; set; }
        public string? Answer { get; set; }
        public int TimeSpent { get; set; }
        public bool ViewedVideo { get; set; }
        public int HomeworkSetId { get; set; }
    }

}
