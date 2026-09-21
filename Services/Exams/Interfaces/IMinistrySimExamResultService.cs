using System.Threading.Tasks;
using QdratNew.Services.Exams.Models;

namespace QdratNew.Services.Exams.Interfaces
{
    // Sprint 14 (MSE-H / H1): استخراج منطق حساب نتيجة/مراجعة اختبار معمل القياس من
    // Areas/Students/Controllers/MinistrySimExamController (Result/QuestionReview) إلى خدمة تقبل studentId صريح،
    // ليعيد استخدامها Admin للاطّلاع على نتيجة أي طالب (Drill-down) دون تكرار نفس منطق الحساب.
    public interface IMinistrySimExamResultService
    {
        Task<MinistrySimExamResultLookup> GetResultAsync(int ministrySimExamId, int studentId);

        Task<MinistrySimExamQuestionReviewLookup> GetQuestionReviewAsync(int ministrySimExamId, int studentId, int stageNumber);
    }
}
