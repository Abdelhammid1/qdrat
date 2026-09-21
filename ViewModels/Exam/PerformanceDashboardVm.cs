namespace QdratNew.ViewModels.Exam
{
    public class PerformanceDashboardVm
    {
        public int TotalExams { get; set; }
        public int TotalStudents { get; set; }
        public double AverageScore { get; set; }
        public double AverageTime { get; set; }

        public List<SectionPerformanceVm> SectionStats { get; set; }
        public List<WeakSectionVm> WeakSections { get; set; }
    }

    public class SectionPerformanceVm
    {
        public int SectionId { get; set; }

        public string SectionName { get; set; }
        public double AvgScore { get; set; }
        public int TotalQuestions { get; set; }
        public double Accuracy { get; set; }


        public string SectionTitle { get; set; }
        public int ExamsCount { get; set; }
        public int Total { get; internal set; }
        public int Correct { get; internal set; }
        public int Wrong { get; internal set; }
        public int Skipped { get; internal set; }
    }



}