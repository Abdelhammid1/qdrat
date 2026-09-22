using QdratNew.Services.Exams.Internal;
using QdratNew.Services.Exams.Models;
using QdratNew.ViewModels.Partner.Exam;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Services.Exams.Interfaces
{
    public interface IPartnerExamGenerationService
    {
        // ===============================
        // 1️⃣ توليد مؤقت (Preview فقط)
        // ===============================
        Task<GeneratedExamResult> GeneratePreview(
            GenerateExamRequestVM request
        );

        // ===============================
        // 2️⃣ حفظ المسودة
        // ===============================
        Task<int> SaveDraft(
            GeneratedExamResult generated,
            int partnerId
        );

        // ===============================
        // 3️⃣ عرض المسودات
        // ===============================
        Task<List<ExamDraftListVM>> GetPartnerDrafts(
            int partnerId
        );

        Task<ExamDraftPreviewVM> GetDraftForPreview(
            int draftId
        );

        // ===============================
        // 4️⃣ تعديل المسودة
        // ===============================
        Task<ReplaceExamDraftQuestionVM> GetReplaceCandidates(
         int draftId,
         Guid oldQuestionId,
         int lessonId
     );


        Task ReplaceQuestion(
            int draftId,
            Guid oldQuestionId,
            Guid newQuestionId
        );

        Task<AddExamDraftQuestionVM> GetAddQuestionCandidates(
            int draftId,
            int lessonId
        );

        Task AddQuestionToDraft(
            int draftId,
            Guid questionId
        );

        // ===============================
        // 5️⃣ إرسال المسودة
        // ===============================
        Task<SendExamDraftVM> PrepareSendDraftVM(
            int draftId,
            int partnerId
        );

        Task SendDraftToBatch(
            SendExamDraftVM model,
            int partnerId
        );

        Task ConfirmStudentExam(
            ConfirmStudentExamVM model
        );
    }
}
