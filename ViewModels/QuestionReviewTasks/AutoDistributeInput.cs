using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.ViewModels.QuestionReviewTasks
{
    /// <summary>
    /// مدخل التوزيع التلقائي (QRT-S7.2): أول N سؤال حسب الفلتر تُوزَّع على عدة مدربين،
    /// وكل مدرب يستلم مهمة مستقلة. نفس مدخل المعاينة والتنفيذ.
    /// </summary>
    public sealed class AutoDistributeInput
    {
        // 150 لا 200: يُضاف اسم المدرب إلى عنوان كل مهمة
        [Required, StringLength(150, MinimumLength = 3)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? AdminNote { get; set; }

        public QuestionReviewTaskPriority Priority { get; set; } = QuestionReviewTaskPriority.Normal;
        public DateTime? DueAtLocal { get; set; }                 // يُحوَّل لـ UTC في الخدمة

        // الفلتر (كله اختياري) + العدد الإجمالي
        public int? CurriculumId { get; set; }
        public int? SectionId { get; set; }
        public int? LessonId { get; set; }

        [Required, Range(1, 500)]
        public int? TakeCount { get; set; }

        [MinLength(1, ErrorMessage = "اختر مدربًا واحدًا على الأقل.")]
        [MaxLength(20, ErrorMessage = "الحد الأقصى 20 مدربًا في التوزيع الواحد.")]
        public List<int> InstructorIds { get; set; } = new();
    }

    /// <summary>سطر معاينة التوزيع: مدرب ← عدد الأسئلة.</summary>
    public sealed record AutoDistributionLineDto(int InstructorId, string InstructorName, int CurrentLoad, int Assigned);

    /// <summary>نتيجة معاينة التوزيع قبل التأكيد (لا تكتب في قاعدة البيانات).</summary>
    public sealed record AutoDistributionPreviewDto(
        int QuestionCount,
        int ExcludedInstructors,
        IReadOnlyList<string> ExcludedInstructorNames,
        IReadOnlyList<AutoDistributionLineDto> Lines);

    /// <summary>خيار منهج لصفحة التوزيع التلقائي.</summary>
    public sealed class AutoDistributePageVm
    {
        public List<AdminFilterOptionVm> Curriculums { get; init; } = new();
    }
}
