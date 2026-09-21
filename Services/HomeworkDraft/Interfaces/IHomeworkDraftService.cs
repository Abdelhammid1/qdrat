using QdratNew.ViewModels.Partner.Homework;
using QdratNew.ViewModels.Partner.HomeworkDraft;
using System.Collections.Generic;

namespace QdratNew.Services.HomeworkDraft.Interfaces
{
    public interface IHomeworkDraftService
    {
        // 📄 قائمة المسودات
        List<HomeworkDraftListVM> GetDrafts(
            int partnerId,
            int subscriptionPeriodId
        );

        // 💾 حفظ مسودة يدوية
        int SaveDraft(
            SaveHomeworkDraftVM model,
            int partnerId,
            int subscriptionPeriodId
        );

        // 🤖 توليد تلقائي
        int GenerateAutoDraft(
        int partnerId,
        int subscriptionPeriodId,
        HomeworkAutoGenerateVM model
    );

        // 🧠 توليد من نموذج احترافي
        int GenerateDraftFromProfessionalModel(
            int partnerId,
            int subscriptionPeriodId,
            HomeworkGenerateFromProfessionalModelVM model
        );

        // 👁️ معاينة المسودة
        HomeworkDraftPreviewVM GetDraftForPreview(int draftId);



        // ===============================
        // ➕ إضافة سؤال
        // ===============================
        List<ReplaceCandidateQuestionVM> GetAddCandidates(
            int draftId,
            int lessonId
        );

        void AddQuestion(
            int draftId,
            Guid questionId
        );

        // ===============================
        // 🔁 استبدال سؤال
        // ===============================
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
