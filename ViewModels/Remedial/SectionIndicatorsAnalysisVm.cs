namespace QdratNew.ViewModels.Remedial
{
    public class SectionIndicatorsAnalysisVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public int StudentId { get; set; }
        public List<IndicatorLessonAnalysisVm> Lessons { get; set; } = new();
    }
}
