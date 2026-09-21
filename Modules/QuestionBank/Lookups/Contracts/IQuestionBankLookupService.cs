using QdratNew.Modules.QuestionBank.Lookups.Dtos;

namespace QdratNew.Modules.QuestionBank.Lookups.Contracts
{
    public interface IQuestionBankLookupService
    {
        Task<List<CurriculumLookupDto>> GetCurriculumsAsync();

        Task<List<SectionLookupDto>> GetSectionsByCurriculumAsync(int curriculumId);

        Task<List<LessonLookupDto>> GetLessonsBySectionAsync(int sectionId);
    }
}
