namespace QdratNew.ViewModels.Partner.Dashboard
{
    public class PartnerDashboardViewModel
    {
        public PartnerStudentKpiViewModel StudentKpis { get; set; } = new();
        public PartnerExamKpiViewModel ExamKpis { get; set; } = new();
        public PartnerHomeworkKpiViewModel HomeworkKpis { get; set; } = new();
        public PartnerInstructorKpiViewModel InstructorKpis { get; set; } = new();
        public PartnerContractKpiViewModel ContractKpis { get; set; } = new();

        public PartnerChartsViewModel Charts { get; set; } = new();

        public List<PartnerBatchSummaryViewModel> Batches { get; set; } = new();
        public List<PartnerInstructorSummaryViewModel> Instructors { get; set; } = new();

        public List<PartnerDecisionAlertViewModel> DecisionAlerts { get; set; } = new();
    }
}
