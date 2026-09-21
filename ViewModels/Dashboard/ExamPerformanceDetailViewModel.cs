namespace QdratNew.ViewModels.Dashboard
{
    public class ExamPerformanceDetailViewModel
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; }
        public string CurriculumTitle { get; set; }
        public List<SectionPerformanceItem> Sections { get; set; } = new();
    }
}
