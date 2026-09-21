namespace QdratNew.ViewModels.StudentAnalysis
{
    public class PerformanceAnalyticsVM
    {
        public double StrengthPercentage { get; set; }
        public double WeaknessPercentage { get; set; }
        public double RiskLevel { get; set; }

        public List<string> CriticalSections { get; set; } = new();
    }
}
