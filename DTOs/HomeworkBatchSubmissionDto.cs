namespace QdratNew.DTOs
{
    public class HomeworkBatchSubmissionDto
    {
        public List<int> HomeworkIds { get; set; } = new();
        public List<string?> SelectedAnswers { get; set; } = new();
    }
}
