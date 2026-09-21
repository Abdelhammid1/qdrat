namespace QdratNew.ViewModels.Exam
{
    public class PerformanceExamCardVm
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = "";
        public string CurriculumTitle { get; set; } = "";
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public bool IsOnline { get; set; }
        public bool IsCompleted { get; set; }
        public double? StudentScore { get; set; }
    }
}
