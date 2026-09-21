using QdratNew.ViewModels.Students;

namespace QdratNew.Services.Exams.Models
{
    // Sprint 14 (MSE-H / H1): نتائج مُحسَّبة عبر IMinistrySimExamResultService — تُميّز حالات الفشل الثلاث
    // التي كان يُفرّقها كود Controller مباشرة (محاولة غير موجودة / غير مكتملة / اختبار غير موجود) بدل استثناءات عامة،
    // ليقرر كل Controller (طالب أو أدمن) كيفية التعامل مع كل حالة حسب سياقه الخاص.
    public enum MinistrySimExamResultStatus
    {
        Ok,
        AttemptNotFound,
        NotCompleted,
        ExamNotFound
    }

    public class MinistrySimExamResultLookup
    {
        public MinistrySimExamResultStatus Status { get; set; }
        public MinistrySimExamResultVm Vm { get; set; }
    }

    public enum MinistrySimExamQuestionReviewStatus
    {
        Ok,
        AttemptNotFound,
        NotCompleted,
        StageNotFound
    }

    public class MinistrySimExamQuestionReviewLookup
    {
        public MinistrySimExamQuestionReviewStatus Status { get; set; }
        public MinistrySimExamQuestionReviewVm Vm { get; set; }
    }
}
