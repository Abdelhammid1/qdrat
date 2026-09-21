using QdratNew.ViewModels.Exam;

namespace QdratNew.ViewModels.Reports
{
    public class StudentExamPerformanceReportVm
    {
        public string StudentName { get; set; }
        public string ExamTitle { get; set; }
        public DateTime ExamDate { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedQuestions { get; set; }
        public double OverallPercent { get; set; }
        public double SolveMinutes { get; set; }
        public double DurationMinutes { get; set; }

        public string SpeedLabel { get; set; }
        public string SpeedNote { get; set; }
        public string TrackCard1 { get; set; }
        public string TrackCard1Desc { get; set; }
        public string TrackCard2 { get; set; }
        public string TrackCard2Desc { get; set; }

        public int ExamAssignmentId { get; set; }
        public int StudentId { get; set; }

        public List<string> IndividualTips { get; set; } = new();

public List<SectionPerformancesVm> Sections { get; set; } = new();



    }
}
