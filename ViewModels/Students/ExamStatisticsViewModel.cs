namespace QdratNew.ViewModels.Students
{
    public class ExamStatisticsViewModel
    {
        public int TotalExams { get; set; }
        public int CompletedExams { get; set; }
        public int PendingExams { get; set; }
        public int LateExams { get; set; }

        // 🧾 قائمة تفصيلية للاستخدام في AllExams
        public List<ExamItemVm> Exams { get; set; } = new();
    }

    public class ExamItemVm
    {
        public int ExamId { get; set; }
        public int AssignmentId { get; set; }
        public string Title { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsSubmitted { get; set; }
        public string StatusText { get; set; }
    }
}
