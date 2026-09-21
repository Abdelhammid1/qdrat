namespace QdratNew.ViewModels.Students
{
    public class StudentAIAnalysisChartViewModel
    {
        public int StudentID { get; set; }
        public string StudentName { get; set; }

        public List<StudentSubjectPerformanceChartViewModel> SubjectCharts { get; set; } = new();
    }
}
