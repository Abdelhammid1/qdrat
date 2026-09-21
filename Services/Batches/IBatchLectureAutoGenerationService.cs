namespace QdratNew.Services.Batches
{
    public interface IBatchLectureAutoGenerationService
    {
        Task GenerateForBatchAsync(int batchId, int courseId, DateTime firstLectureStartDateTime, int? lectureDurationMinutes = null);
    }
}
