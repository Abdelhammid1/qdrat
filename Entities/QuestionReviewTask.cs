using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    /// <summary>
    /// مهمة مراجعة: دفعة أسئلة من البنك يُسندها الأدمن لمدرب محدد لاعتمادها.
    /// حالة الاعتماد الفعلية تبقى على Question (IsReviewed)؛ المهمة طبقة تتبّع وحجز فوقها.
    /// </summary>
    public class QuestionReviewTask
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string Code { get; set; } = string.Empty;          // QRT-2026-0001

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? AdminNote { get; set; }

        public int InstructorId { get; set; }
        public Instructor? Instructor { get; set; }

        public int? CurriculumId { get; set; }                    // للعرض والفلترة فقط
        public Curriculum? Curriculum { get; set; }

        public int? ParentTaskId { get; set; }                    // عند إعادة إسناد المتبقي لمدرب آخر
        public QuestionReviewTask? ParentTask { get; set; }

        public QuestionReviewTaskStatus Status { get; set; } = QuestionReviewTaskStatus.Assigned;
        public QuestionReviewTaskPriority Priority { get; set; } = QuestionReviewTaskPriority.Normal;

        public DateTime? DueAtUtc { get; set; }

        [Required, MaxLength(450)]
        public string CreatedByUserId { get; set; } = string.Empty;
        [MaxLength(200)]
        public string? CreatedByName { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? ClosedAtUtc { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
        [MaxLength(500)]
        public string? CancelReason { get; set; }
        public DateTime? LastReminderAtUtc { get; set; }

        // عدادات تُعاد حسابها (D10)
        public int TotalItems { get; set; }
        public int PendingItems { get; set; }
        public int ApprovedItems { get; set; }       // Approved + EditedAndApproved + ApprovedByAdmin
        public int ReturnedItems { get; set; }       // Returned (غير معالج)
        public int RemovedItems { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ICollection<QuestionReviewTaskItem> Items { get; set; } = new List<QuestionReviewTaskItem>();
    }
}
