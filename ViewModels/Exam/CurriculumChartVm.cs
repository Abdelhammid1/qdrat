namespace QdratNew.ViewModels.Exam
{
    public class CurriculumChartVm
    {
        public string CurriculumTitle { get; set; }
        public List<ExamSectionPerformanceVm> Sections { get; set; } = new();
    }
}
