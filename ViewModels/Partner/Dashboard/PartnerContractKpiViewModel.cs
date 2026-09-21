namespace QdratNew.ViewModels.Partner.Dashboard
{
    public class PartnerContractKpiViewModel
    {
        public string ContractStatus { get; set; } = string.Empty;

        public DateTime EndDate { get; set; }
        public int RemainingDays { get; set; }

        public int MaxAllowedStudents { get; set; }
        public int UsedStudents { get; set; }

        public decimal StudentUsagePercentage { get; set; }
        public decimal TimeUsagePercentage { get; set; }
    }
}
