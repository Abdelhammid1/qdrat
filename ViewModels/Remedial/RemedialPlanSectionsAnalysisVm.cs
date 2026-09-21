namespace QdratNew.ViewModels.Remedial
{
    public class RemedialPlanSectionsAnalysisVm
    {
        public int PlanId { get; set; }
        public string StudentName { get; set; }
        public string PlanTitle { get; set; }
        public List<WeakSectionAnalysisVm> Sections { get; set; }
    }

}
