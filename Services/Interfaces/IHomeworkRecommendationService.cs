using QdratNew.ViewModels.Homework;

namespace QdratNew.Services.Interfaces
{
    public interface IHomeworkRecommendationService
    {
        HomeworkRecommendationVm GenerateRecommendation(HomeworkAnalyticsVm analytics);
    }
}
