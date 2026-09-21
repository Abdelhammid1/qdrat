namespace QdratNew.ViewModels.Instructor.Exam
{
    public class InstructorSentExamItemVM
    {
        public int ExamId { get; set; }

        public string Title { get; set; }

        public string ExamType { get; set; }

        public string TargetName { get; set; }

        public DateTime? StartAt { get; set; }

        public DateTime? EndAt { get; set; }

        public int DurationMinutes { get; set; }
        public int ExamAssignmentId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
