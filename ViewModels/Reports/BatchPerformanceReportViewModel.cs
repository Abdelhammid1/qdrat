namespace QdratNew.ViewModels.Reports
{
    public class BatchPerformanceReportViewModel
    {
        public string BatchName { get; set; }
        public string ExamTitle { get; set; }
        public string CurriculumTitle { get; set; }
        public double AveragePercent { get; set; }
        public List<RemedialStudentVm> RemedialStudents { get; set; } = new();
        public int TotalStudents { get; set; }
        public int BatchId { get; set; }
        public int CompletedCount { get; set; }
        public double HighestScore { get; set; }
        public double LowestScore { get; set; }
        public List<SectionPerformanceSummary> Sections { get; set; } = new();
        public List<StudentPerformanceSummary> Students { get; set; } = new();

        public List<StudentPerformanceVm> StudentsResults { get; set; } = new();


        public int ExamId { get; set; }
   
        public int TestedStudents { get; set; }
        public int NotTestedStudents { get; set; }
        public int PassedStudents { get; set; }
        public int FailedStudents { get; set; }
    

        public List<SectionSummaryVm> SectionStats { get; set; } = new();
        public List<SectionSummaryVm> TopSections { get; set; } = new();
        public List<SectionSummaryVm> WeakSections { get; set; } = new();



    }


    public class StudentRemedialVm
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public List<string> FailedSections { get; set; } = new();
    }
    public class SectionSummaryVm
    {
        public string SectionTitle { get; set; }
        public double AvgScore { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
    }
    public class SectionPerformanceSummary
    {
        public string SectionTitle { get; set; }
        public int TotalQuestions { get; set; }
        public double AveragePercent { get; set; }
        public double HighestPercent { get; set; }
        public double LowestPercent { get; set; }
    }

    public class StudentPerformanceSummary
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public double OverallPercent { get; set; }
        public double QuantPercent { get; set; }
        public double VerbalPercent { get; set; }
        public string WeakestSection { get; set; }
    }
}
