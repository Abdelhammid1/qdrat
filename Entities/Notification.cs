using QdratNew.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class Notification
    {
        public int NotificationId { get; set; }

        public string? UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        public int? StudentID { get; set; }
        public int? ParentID { get; set; }

        public required string Message { get; set; }
        public DateTime SentAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;

        public virtual QdratNew.Entities.Student Student { get; set; }
        public virtual Parent Parent { get; set; }

        public NotificationCategory Category { get; set; }

        // ✅ الإضافة الجديدة المطلوبة
        public string? TargetUrl { get; set; }  // رابط التنقل عند الضغط

        public int? SentByInstructorId { get; set; }
        [ForeignKey("SentByInstructorId")]
        public Instructor? SentByInstructor { get; set; }

        public DateTime? MeetingAt { get; set; }

    }

}