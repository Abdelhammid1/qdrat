using QdratNew.Services.Exams.Models;
using QdratNew.ViewModels.Partner.Exam;

namespace QdratNew.Services.Exams.Interfaces
{
    public interface IPartnerExamDraftService
    {
        // توليد مؤقت (Preview فقط)
        GeneratedExamResult GeneratePreview(GenerateExamRequestVM request);

        // حفظ فعلي للمسودة
        int SaveDraft(GeneratedExamResult generated, int partnerId);

        // قراءة مسودة
        ExamDraftPreviewVM GetDraft(int draftId, int partnerId);

        // تجهيز الإرسال
        SendExamDraftVM PrepareSend(int draftId, int partnerId);

        // إرسال المسودة
        void SendToBatches(SendExamDraftVM model, int partnerId);

        // استبدال سؤال
        ReplaceExamDraftQuestionVM GetReplaceCandidates(
            int draftId,
            Guid oldQuestionId,
            int lessonId);

        void ReplaceQuestion(
            int draftId,
            Guid oldQuestionId,
            Guid newQuestionId);
    }
}
