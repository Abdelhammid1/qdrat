using QdratNew.ViewModels.HomeworkGeneration;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IHomeworkGenerationService
    {
        // =====================================================
        // 1️⃣ إنشاء مسودة واجب (Draft) من مؤشرات محددة
        // =====================================================
        Task<HomeworkGenerationDraftResult> GenerateDraftAsync(
            HomeworkGenerationRequest request
        );

        // =====================================================
        // 2️⃣ جلب مسودة واجب كاملة للمراجعة
        // =====================================================
        Task<HomeworkGenerationDraftViewModel?> GetDraftAsync(
            int draftId
        );

        // =====================================================
        // 3️⃣ تعديل عدد الأسئلة لمؤشر معين داخل المسودة
        // =====================================================
        Task<bool> UpdateLessonQuestionsCountAsync(
            int draftId,
            int lessonId,
            int newQuestionsCount
        );

        // =====================================================
        // 4️⃣ استبدال سؤال داخل المسودة بسؤال آخر من بنك الأسئلة
        // =====================================================
        Task<bool> ReplaceQuestionAsync(
            int draftId,
            int lessonId,
            int oldQuestionId,
            int newQuestionId
        );

        // =====================================================
        // 5️⃣ حذف سؤال من المسودة
        // =====================================================
        Task<bool> RemoveQuestionAsync(
            int draftId,
            int lessonId,
            int questionId
        );

        // =====================================================
        // 6️⃣ إضافة سؤال يدويًا من بنك الأسئلة
        // =====================================================
        Task<bool> AddQuestionAsync(
            int draftId,
            int lessonId,
            int questionId
        );

        // =====================================================
        // 7️⃣ تأكيد المسودة وتحويلها إلى واجب فعلي
        // =====================================================
        Task<HomeworkPublishResult> ConfirmAndPublishAsync(
            int draftId,
            HomeworkPublishRequest publishRequest
        );

        // =====================================================
        // 8️⃣ إلغاء المسودة
        // =====================================================
        Task<bool> CancelDraftAsync(
            int draftId
        );
    }
}
