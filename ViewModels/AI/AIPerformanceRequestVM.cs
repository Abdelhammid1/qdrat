namespace QdratNew.ViewModels.AI
{
    public class AIPerformanceRequestVM
    {
        public int StudentId { get; set; }
        public List<string> WrongQuestions { get; set; } = new();
        public double LastExamScore { get; set; }
        public double AvgBatchScore { get; set; }
        public List<string> WeakSections { get; set; } = new();
    }
}
