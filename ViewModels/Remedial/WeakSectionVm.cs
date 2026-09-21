namespace QdratNew.ViewModels.Remedial
{
    public class WeakSectionVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public double ScorePercent { get; set; }
        public bool HasRemedialPlan { get; set; }
        public string CurriculumTitle { get; internal set; }
    }



}
