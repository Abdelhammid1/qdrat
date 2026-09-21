namespace QdratNew.Services.Interfaces
{
    public interface IAttendanceMonitoringService
    {
        Task CheckMissingBatchLessonCompletionsAsync();
    }
}
