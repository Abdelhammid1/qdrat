using QdratNew.ViewModels.AI;

namespace QdratNew.ViewModels.Students
{
    public class StudentSidebarAnalyticsViewModel
    {
        public List<HomeworkScoreEntryViewModel> HomeworkScoresOverTime { get; set; }
        public List<HomeworkScoreEntryViewModel> ExamScoresOverTime { get; set; }

        public int OverallSuccessRate { get; set; }
        public List<SmartRecommendation> AITips { get; set; } = new();

    }


}
