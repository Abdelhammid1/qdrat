namespace QdratNew.ViewModels.Instructor.Exam
{
    public class InstructorExamSendVM
    {
        public int DraftId { get; set; }

        public List<int> BatchIds { get; set; } = new();
        public List<SelectItemVM> Batches { get; set; } = new();

        public List<int> StudentIds { get; set; } = new();
        public List<SelectItemVM> Students { get; set; } = new();

        public string ExamTitle { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int DurationMinutes { get; set; }
        public string ExamMode { get; set; } = "online";
    }
}
