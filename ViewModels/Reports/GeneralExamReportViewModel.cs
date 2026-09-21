namespace QdratNew.ViewModels.Reports
{
    public class GeneralExamReportViewModel
    {
        public string StudentName { get; set; }
        public string ExamTitle { get; set; }
        public DateTime ExamDate { get; set; }

        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }

        public double OverallPercent { get; set; }

        public double TotalMinutes { get; set; }
        public double SolveMinutes { get; set; }

        public List<string>? QuantLabels { get; set; }
        public List<int>? QuantCorrectCounts { get; set; }
        public List<int>? QuantWrongCounts { get; set; }

        public List<string>? VerbalLabels { get; set; }
        public List<int>? VerbalCorrectCounts { get; set; }
        public List<int>? VerbalWrongCounts { get; set; }
    }
}
