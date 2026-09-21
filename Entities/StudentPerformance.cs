using QdratNew.Entities;
using QdratNew.Entities;
using QdratNew.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudentPerformance
    {
        [Key]
        public int Id { get; set; }  // 🔹 المفتاح الأساسي

        [Required]
        public int StudentID { get; set; }  // 🔹 معرف الطالب

        [Required]
        [Range(0, 100, ErrorMessage = "يجب أن تكون الدرجة بين 0 و 100.")]
        public double Score { get; set; }  // 🔹 درجة الطالب

       

        [Required]
        public DateTime ExamDate { get; set; } = DateTime.UtcNow;  // 🔹 تاريخ الامتحان

        // 🔹 مستوى الطالب تلقائيًا حسب الدرجة
        public string Level
        {
            get
            {
                if (Score >= 90) return "ممتاز";
                if (Score >= 75) return "جيد جدًا";
                if (Score >= 60) return "جيد";
                if (Score >= 50) return "مقبول";
                return "ضعيف";
            }
        }

        // 🔹 خصائص إضافية للأداء
        [Range(0, 1000)]
        public float StudyHours { get; set; } = 0;

        [Range(0, 500)]
        public int ExercisesCompleted { get; set; } = 0;

        [Range(0, 365)]
        public int AttendanceCount { get; set; } = 0;

        [Range(0, 100)]
        public float EngagementRate { get; set; } = 0;

        public string? WeakTopics { get; set; }

        // ✅ العلاقة مع الطالب
        public virtual Student Student { get; set; }

        // ✅ العلاقة مع المنهج
        public int? CurriculumId { get; set; }   // ✅ تسمح بالقيم الفارغة
        public Curriculum Curriculum { get; set; }

        // ✅ العلاقة الجديدة مع المحور (Section)
        public int? SectionId { get; set; }

        [ForeignKey("SectionId")]
        public Section? Section { get; set; }
        public ExamType ExamType { get; set; }  // نوع الاختبار (Placement, PerformanceScale, Final)
        public int? ExamId { get; set; }
        public Exam? Exam { get; set; }
        public double EngagementScore { get; set; } = 0; // نسبة الحضور والمشاركة (0 إلى 100)

        public PerformanceActivityType ActivityType { get; set; }

    }
}
