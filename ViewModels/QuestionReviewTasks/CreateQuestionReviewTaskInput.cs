using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.ViewModels.QuestionReviewTasks
{
    public sealed class CreateQuestionReviewTaskInput
    {
        [Required, StringLength(200, MinimumLength = 3)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? AdminNote { get; set; }

        [Range(1, int.MaxValue)]
        public int InstructorId { get; set; }

        public QuestionReviewTaskPriority Priority { get; set; } = QuestionReviewTaskPriority.Normal;
        public DateTime? DueAtLocal { get; set; }                 // يُحوَّل لـ UTC في الخدمة

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
