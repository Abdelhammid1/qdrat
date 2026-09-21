using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Services.Exams.Helpers
{
    public interface IExamQuestionSelectorService
    {
        Task<List<Question>> SelectQuestionsAsync(
            int curriculumId,
            int total,
            int easy,
            int medium,
            int hard,
            QuestionUsageType targetUsageType,
            int studentId = 0,
            List<Guid>? previouslyUsedQuestionIds = null);
    }
}
