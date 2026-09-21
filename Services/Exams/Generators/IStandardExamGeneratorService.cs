namespace QdratNew.Services.Exams.Generators
{
    public interface IStandardExamGeneratorService
    {
        Task<int> GenerateExamForCurriculumAsync(
            int curriculumId,
            int batchId,
            int sectionId,
            int? createdByInstructorId = null);
    }
}
