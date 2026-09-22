using QdratNew.ViewModels.Partner.Homework;
using QdratNew.ViewModels.Partner.HomeworkDraft;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Services.HomeworkDraft.Interfaces
{
    public interface IHomeworkDraftService
    {
        // 📄 قائمة المسودات
        Task<List<HomeworkDraftListVM>> GetDrafts(
            int partnerId,
            int subscriptionPeriodId
        );

        // 💾 حفظ مسودة يدوية
        Task<int> SaveDraft(
            SaveHomeworkDraftVM model,
            int partnerId,
            int subscriptionPeriodId
        );

        // 🤖 توليد تلقائي
        Task<int> GenerateAutoDraft(
        int partnerId,
        int subscriptionPeriodId,
        HomeworkAutoGenerateVM model
    );

        // 🧠 توليد من نموذج احترافي
        Task<int> GenerateDraftFromProfessionalModel(
            int partnerId,
            int subscriptionPeriodId,
            HomeworkGenerateFromProfessionalModelVM model
        );

        // 👁️ معاينة المسودة
        Task<HomeworkDraftPreviewVM> GetDraftForPreview(int draftId);



        // ===============================
        // ➕ إضافة سؤال
        // ===============================
        Task<List<ReplaceCandidateQuestionVM>> GetAddCandidates(
            int draftId,
            int lessonId
        );

        Task AddQuestion(
            int draftId,
            Guid questionId
        );

        // ===============================
        // 🔁 استبدال سؤال
        // ===============================
        Task<ReplaceHomeworkDraftQuestionVM> GetReplaceCandidates(
            int draftId,
            Guid oldQuestionId,
            int lessonId
        );

        Task ReplaceQuestion(
            int draftId,
            Guid oldQuestionId,
            Guid newQuestionId
        );
    }
}
