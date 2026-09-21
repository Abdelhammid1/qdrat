using QdratNew.ViewModels.Admin.Analytics;

namespace QdratNew.Services.Analytics
{
    public interface IBatchAnalyticsService
    {
        Task<BatchPerformanceDetailsVM?> BuildBatchPerformanceAsync(
            int batchId, int? curriculumId, int? instructorId);

        Task<WeakLessonDetailsVM?> BuildWeakLessonAsync(
            int lessonId, int? curriculumId, int? batchId, int? instructorId);
    }
}
