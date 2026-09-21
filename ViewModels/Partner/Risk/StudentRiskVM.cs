namespace QdratNew.ViewModels.Partner.Risk
{
    public class StudentRiskVM
    {
        public int StudentId { get; set; }
        public string Name { get; set; }

        public int RiskScore { get; set; } // من 100

        public string RiskLevel { get; set; } // Safe / Warning / Risk

        public string Reason { get; set; }
    }
}