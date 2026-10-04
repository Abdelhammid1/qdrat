using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.QuestionReviewTasks
{
    /// <summary>
    /// مدخل جلب المدربين المؤهلين لإسناد مهمة مراجعة (QRT-S2.3).
    /// نفس وضعَي الإنشاء: تحديد يدوي، أو أول N سؤال حسب الفلتر.
    /// </summary>
    public sealed class EligibleInstructorsInput
    {
        // وضع 1: تحديد يدوي
        public List<Guid> SelectedQuestionIds { get; set; } = new();

        // وضع 2: أول N حسب الفلتر (يُستخدم إذا SelectedQuestionIds فارغة)
        public int? CurriculumId { get; set; }
        public int? SectionId { get; set; }
        public int? LessonId { get; set; }
        [Range(1, 500)]
        public int? TakeCount { get; set; }
    }
}
