using QdratNew.Services.Exams.Internal;
using QdratNew.Services.Exams.Models;
using QdratNew.ViewModels.Partner.Exam;
using System;
using System.Collections.Generic;

namespace QdratNew.Services.Exams.Interfaces
{
    public interface IPartnerExamGenerationService
    {
        // ===============================
        // 1️⃣ توليد مؤقت (Preview فقط)
        // ===============================
        GeneratedExamResult GeneratePreview(
            GenerateExamRequestVM request
        );

        // ===============================
        // 2️⃣ حفظ المسودة
        // ===============================
        int SaveDraft(
            GeneratedExamResult generated,
            int partnerId
        );

        // ===============================
        // 3️⃣ عرض المسودات
        // ===============================
        List<ExamDraftListVM> GetPartnerDrafts(
            int partnerId
        );

        ExamDraftPreviewVM GetDraftForPreview(
            int draftId
        );

        // ===============================
        // 4️⃣ تعديل المسودة
        // ===============================
        ReplaceExamDraftQuestionVM GetReplaceCandidates(
         int draftId,
         Guid oldQuestionId,
         int lessonId
     );


        void ReplaceQuestion(
            int draftId,
            Guid oldQuestionId,
            Guid newQuestionId
        );

        AddExamDraftQuestionVM GetAddQuestionCandidates(
            int draftId,
            int lessonId
        );

        void AddQuestionToDraft(
            int draftId,
            Guid questionId
        );

        // ===============================
        // 5️⃣ إرسال المسودة
        // ===============================
        SendExamDraftVM PrepareSendDraftVM(
            int draftId,
            int partnerId
        );

        void SendDraftToBatch(
            SendExamDraftVM model,
            int partnerId
        );

        void ConfirmStudentExam(
            ConfirmStudentExamVM model
        );
    }
}
