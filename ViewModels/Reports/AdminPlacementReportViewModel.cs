namespace QdratNew.ViewModels.Reports
{
    public class AdminPlacementReportViewModel
    {
        public string ExamTitle { get; set; }
        public string StudentName { get; set; }

        public int StudentId { get; set; }

        public DateTime ExamDate { get; set; }
        public double OverallPercent { get; set; }
        public double AverageSolveMinutes { get; set; }
        public List<string> QuantLabels { get; set; } = new();
        public List<double> QuantScores { get; set; } = new();
        public List<string> VerbalLabels { get; set; } = new();
        public List<double> VerbalScores { get; set; } = new();
        public List<string> WeakestTopics { get; set; } = new();
        public List<string> StrongestTopics { get; set; } = new();
        public string RecommendationText { get; set; }
        public string AdminDecisionTip { get; set; }

        // 🆕 عدد إجابات "لا أعرف الإجابة"
        public int DontKnowAnswers { get; set; }

    }

    public class AdminPlacementBatchReportViewModel
    {
        public string BatchName { get; set; }
        public string ExamTitle { get; set; }
        public DateTime ExamDate { get; set; }
        public double BatchAverage { get; set; }
        public double HighestScore { get; set; }
        public double LowestScore { get; set; }
        public List<string> QuantLabels { get; set; } = new();
        public List<double> QuantScores { get; set; } = new();
        public List<string> VerbalLabels { get; set; } = new();
        public List<double> VerbalScores { get; set; } = new();
        public string RecommendationText { get; set; }
        public string AdminDecisionTip { get; set; }
        public List<StudentPlacementResultVm> StudentResults { get; set; } = new();
    }

    public class StudentPlacementResultVm
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public double Percent { get; set; }
        public string Evaluation { get; set; }
    }

}
