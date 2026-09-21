namespace QdratNew.ViewModels.Homework
{
    public class HomeworkRecommendationViewModel
    {
        public string SpeedLabel { get; set; } = "";
        public string SpeedNote { get; set; } = "";
        public string PerformanceSummary { get; set; } = "";
        public List<string> IndividualTips { get; set; } = new();
    }
}
