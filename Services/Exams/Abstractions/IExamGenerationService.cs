namespace QdratNew.Services.Exams.Abstractions
{
    public interface IExamGenerationService
    {
        Task<int> GenerateExamDraftAsync(int instructorId, int curriculumId, string title);
    }
}