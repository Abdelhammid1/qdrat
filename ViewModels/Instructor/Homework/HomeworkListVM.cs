namespace QdratNew.ViewModels.Instructor.Homework
{
    public class HomeworkListVM
    {
        public int HomeworkSetId { get; set; }

        public string Title { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? StartAt { get; set; }
        public List<string> Batches { get; set; } = new();
        public int TotalStudents { get; set; }
        public bool IsClosed { get; set; }
        public int SubmittedStudents { get; set; }
    }
}
