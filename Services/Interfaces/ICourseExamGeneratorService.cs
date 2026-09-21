namespace QdratNew.Services.Interfaces
{
    public interface ICourseExamGeneratorService
    {
        Task CheckAndGenerateCourseExamAsync(int batchId, int courseId);
    }
}
