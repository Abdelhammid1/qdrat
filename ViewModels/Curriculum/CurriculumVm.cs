namespace QdratNew.ViewModels.Exam
{
    public class CurriculumVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public double AverageScore { get; set; }
        public int ExamsCount { get; set; }
    }
}
