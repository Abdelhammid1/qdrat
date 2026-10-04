using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.QuestionReviewTasks
{
    // QRT-S6: مدخلات إدارة دورة الحياة (الأدمن). نماذج مخصّصة تمنع Overposting.

    public sealed class ResolveReturnedInput
    {
        [Range(1, long.MaxValue)]
        public long ItemId { get; set; }

        /// <summary>1 = اعتماد كما هو، 2 = إعادة للمسبح، 3 = رفض نهائي (قيم ReturnResolution).</summary>
        [Range(1, 3)]
        public int Resolution { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }
    }

    public sealed class RemoveReviewItemsInput
    {
        [Range(1, int.MaxValue)]
        public int TaskId { get; set; }

        [Required, MinLength(1), MaxLength(500)]
        public List<long> ItemIds { get; set; } = new();
    }

    public sealed class ReassignReviewTaskInput
    {
        [Range(1, int.MaxValue)]
        public int TaskId { get; set; }

        [Range(1, int.MaxValue)]
        public int NewInstructorId { get; set; }
    }

    public sealed class CancelReviewTaskInput
    {
        [Range(1, int.MaxValue)]
        public int TaskId { get; set; }

        [Required, StringLength(500, MinimumLength = 5)]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class ExtendReviewTaskDueInput
    {
        [Range(1, int.MaxValue)]
        public int TaskId { get; set; }

        /// <summary>بتوقيت Arab Standard Time؛ null = إزالة الموعد.</summary>
        public DateTime? DueAtLocal { get; set; }
    }
}
