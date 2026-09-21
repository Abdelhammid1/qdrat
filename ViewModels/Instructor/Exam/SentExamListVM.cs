namespace QdratNew.ViewModels.Instructor.Exam
{
    public class SentExamListVM
    {
        public int ExamId { get; set; }

        public string Title { get; set; }

        public string Type { get; set; }

        public string TargetName { get; set; }

        public DateTime? StartAt { get; set; }

        public DateTime? EndAt { get; set; }

        public int DurationMinutes { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
