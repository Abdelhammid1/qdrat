using QdratNew.Modules.QuestionBank.Insights.Dtos;

namespace QdratNew.Modules.QuestionBank.Insights.Contracts
{
    public interface IQuestionBankInsightsService
    {
        Task<QuestionStatusInsightDto> GetStatusInsightAsync(
            int? curriculumId,
            int? sectionId,
            int? lessonId);

        Task<QuestionQualityInsightDto> GetQualityInsightAsync(
            int? curriculumId,
            int? sectionId,
            int? lessonId);

        Task<List<QuestionCoverageInsightDto>> GetCoverageInsightAsync();
    }
}
