using QdratNew.Modules.QuestionBank.Read.Dtos;

namespace QdratNew.Modules.QuestionBank.Read.Contracts
{
    public interface IQuestionBankReadService
    {
        Task<QuestionBankQueryResultDto> QueryAsync(QuestionBankQueryRequestDto request);

        Task<QuestionBankStatsDto> GetStatsAsync(int? curriculumId, int? sectionId);
    }
}
