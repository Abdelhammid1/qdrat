namespace QdratNew.ViewModels.Exam
{
    public class WeakSectionVm
    {
        public int SectionId { get; set; }
        public string SectionName { get; set; }
        public double Accuracy { get; set; }
        public bool Trained { get; set; }



        public double ScorePercent { get; set; }
        public int StudentsBelowPass { get; set; }
        public object SectionTitle { get; internal set; }
        public bool HasRemedialPlan { get; internal set; }
    }
}
