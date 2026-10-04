using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    public class QuestionReviewTaskItem
    {
        public long Id { get; set; }

        public int TaskId { get; set; }
        public QuestionReviewTask? Task { get; set; }

        public Guid QuestionId { get; set; }
        public Question? Question { get; set; }

        public int SortOrder { get; set; }

        public QuestionReviewTaskItemStatus Status { get; set; } = QuestionReviewTaskItemStatus.Pending;

        /// <summary>
        /// true فقط عندما Status = Pending والمهمة غير ملغاة/مغلقة.
        /// عليه Unique Filtered Index → سؤال واحد لا يُحجز لمهمتين في نفس الوقت.
        /// </summary>
        public bool IsLockActive { get; set; } = true;

        public DateTime? ActionAtUtc { get; set; }
        [MaxLength(450)]
        public string? ActionByUserId { get; set; }
        [MaxLength(200)]
        public string? ActionByName { get; set; }

        [MaxLength(500)]
        public string? ReturnNote { get; set; }           // إلزامية عند Returned
        [MaxLength(500)]
        public string? AdminResolutionNote { get; set; }

        [MaxLength(100)]
        public string? ReferenceNumberSnapshot { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
