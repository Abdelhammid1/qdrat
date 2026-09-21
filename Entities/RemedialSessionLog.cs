using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class RemedialSessionLog
    {
        [Key]
        public int Id { get; set; }

        // 🔹 الطالب
        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public Student Student { get; set; }

        // 🔹 الجلسة العلاجية
        [ForeignKey("Session")]
        public int SessionId { get; set; }
        public RemedialSession Session { get; set; }

        // 🔹 الفيديو المرتبط (من RemedialLesson أو RemedialVideo)
        public int? VideoId { get; set; }

        // 🔹 وقت بداية المشاهدة
        public DateTime WatchStart { get; set; } = DateTime.UtcNow;

        // 🔹 وقت انتهاء المشاهدة
        public DateTime? WatchEnd { get; set; }

        // 🔹 المدة التي شاهدها فعليًا بالثواني
        public int DurationWatched { get; set; }

        // 🔹 هل أنهى الفيديو بالكامل
        public bool IsCompleted { get; set; } = false;

        // 🔹 رقم الـ IP أو الجهاز (اختياري لتحليل السلوك لاحقًا)
        [StringLength(100)]
        public string? DeviceInfo { get; set; }

        // 🔹 آخر تحديث
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
