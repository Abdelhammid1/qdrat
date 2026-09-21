using QdratNew.ViewModels.Homework;

namespace QdratNew.ViewModels.Students
{
    public class ExamReportViewModel
    {
        public int ExamAssignmentId { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }

        public List<SectionPerformanceEntry> SectionDetails { get; set; } = new();

        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
    }
}
