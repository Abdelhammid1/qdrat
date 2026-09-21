namespace QdratNew.ViewModels.StudentAnalysis
{
    public class PerformanceRawDataVM
    {
        public int StudentId { get; set; }
        public double LastExamScore { get; set; }
        public double AvgBatchScore { get; set; }

        public List<string> WrongQuestions { get; set; } = new();
        public List<string> WeakSections { get; set; } = new();
    }
}
