using QdratNew.ViewModels.Analytics;

namespace QdratNew.ViewModels.Admin.Analytics
{
    public class EarlyInterventionDashboardVM
    {
        // KPI
        public int TotalWeakLessons { get; set; }
        public int TotalAffectedStudents { get; set; }
        public int TotalAffectedBatches { get; set; }
        public double AverageWeakness { get; set; }

        // Core Data
        public List<WeakLessonVM> WeakLessons { get; set; } = new();
        public List<RemedialActionVM> SuggestedActions { get; set; } = new();

        // Risk
        public int HighRiskLessons { get; set; }
        public int MediumRiskLessons { get; set; }
        public int LowRiskLessons { get; set; }
    }
}