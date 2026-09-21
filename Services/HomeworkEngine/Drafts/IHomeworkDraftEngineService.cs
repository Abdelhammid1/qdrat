using QdratNew.ViewModels.Partner.Homework;
using QdratNew.ViewModels.Partner.HomeworkDraft;

namespace QdratNew.Services.HomeworkEngine.Drafts
{
    public interface IHomeworkDraftEngineService
    {
        List<HomeworkDraftListVM> GetDrafts(
            int ownerId,
            int? subscriptionPeriodId = null
        );

        int GenerateAutoDraft(
            int ownerId,
            int? subscriptionPeriodId,
            HomeworkAutoGenerateVM model
        );

        HomeworkDraftPreviewVM GetDraftForPreview(int draftId);

        List<QuestionCandidateVM> GetAddCandidates(
            int draftId,
            int lessonId
        );

        void AddQuestion(
            int draftId,
            Guid questionId
        );

        ReplaceHomeworkDraftQuestionVM GetReplaceCandidates(
            int draftId,
            Guid oldQuestionId,
            int lessonId
        );

        void ReplaceQuestion(
            int draftId,
            Guid oldQuestionId,
            Guid newQuestionId
        );
    }
}