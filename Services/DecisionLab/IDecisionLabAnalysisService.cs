using QdratNew.ViewModels.Admin.DecisionLab;

namespace QdratNew.Services.DecisionLab
{
    public interface IDecisionLabAnalysisService
    {
        Task<DecisionLabIndexViewModel> BuildIndexAsync(
            int? batchId,
            int? curriculumId,
            DateTime? fromDate,
            DateTime? toDate);

        Task<DecisionLabBatchAnalysisViewModel?> AnalyzeBatchAsync(
            int batchId,
            int? curriculumId,
            DateTime? fromDate,
            DateTime? toDate);

        Task<IReadOnlyList<DecisionLabWeakLessonViewModel>> GetWeakLessonsAsync(
            int batchId,
            int? curriculumId,
            DateTime? fromDate,
            DateTime? toDate);

        Task<IReadOnlyList<DecisionLabHighRiskQuestionViewModel>> GetHighRiskQuestionsAsync(
            int batchId,
            int? curriculumId,
            DateTime? fromDate,
            DateTime? toDate);
    }
}
