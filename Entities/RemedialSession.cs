using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QdratNew.Entities
{
    public class RemedialSession
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Student")]
        public int StudentID { get; set; }
        public Student Student { get; set; }

        [ForeignKey("RemedialPlan")]
        public int RemedialPlanId { get; set; }
        public RemedialPlan RemedialPlan { get; set; }

        // ✅ عنوان المحور (اختياري)
        public string? SectionTitle { get; set; }

        // ✅ موضوع الجلسة (اختياري)
        public string? Subject { get; set; }

        // 🗓️ تاريخ الجلسة
        public DateTime ScheduledDate { get; set; }

        // 🕒 وقت البداية والنهاية (اختياري)
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }

        // 💺 عدد المقاعد (اختياري)
        public int? TotalSeats { get; set; } = 0;
        public int? ReservedSeats { get; set; } = 0;

        public int? AttendedStudents { get; set; } = 0;
        public int? AbsentStudents { get; set; } = 0;

        // 🏫 اسم القاعة (يجب أن يقبل null)
        public string? RoomName { get; set; } = string.Empty;

        // 🔗 قائمة الطلاب الحاضرين (افتراضيًا فارغة)
        public ICollection<Student> StudentsAttended { get; set; } = new List<Student>();

        public bool IsCompleted { get; set; } = false;
        public bool? IsMandatory { get; set; } = false;

        [Precision(10, 2)]
        public decimal? Fee { get; set; }

        [ForeignKey("Instructor")]
        public int? InstructorId { get; set; }
        public Instructor? Instructor { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsConfirmed { get; set; } = false;

        // ✅ ربط الجلسة بمحور محدد (Section)
        [ForeignKey("Section")]
        public int? SectionId { get; set; }
        public Section? Section { get; set; }

        // ✅ مصادر تعليمية خاصة بالجلسة (اجعلها قابلة للنول)
        public string? Resources { get; set; } = string.Empty;

        // ✅ ربط الغرفة
        public int? StudyRoomId { get; set; }
        public StudyRoom? StudyRoom { get; set; }

        // 🔐 كود الوصول (افتراضي 6 أرقام)
        [MaxLength(6)]
        public string AccessCode { get; set; } = GenerateNumericCode();

        private static string GenerateNumericCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        public bool? IsAIRecommended { get; set; } = false;

        // ✅ ربط الجلسة بالمؤشر (الدرس)
        [ForeignKey("Lesson")]
        public int? LessonId { get; set; }
        public Lesson? Lesson { get; set; }

        public DateTime? CompletedAt { get; set; }
        public string? Notes { get; set; }

        // ✅ ربط الجلسة بالاختبارات المصغّرة
        public int? RemedialQuizId { get; set; }
        public RemedialQuiz? RemedialQuiz { get; set; }




        // 🟢 الحقل الخاص بموعد تأكيد الحضور
        public DateTime? ConfirmedAt { get; set; }

  
    }
}
