using QdratNew.ViewModels.Instructor.Homework;

namespace QdratNew.Services.HomeworkAnalytics
{
    public interface IHomeworkAnalyticsService
    {
        Task<HomeworkGlobalReportVM> BuildGlobalReportAsync(int homeworkSetId);
    }

}