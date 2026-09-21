using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class PerformanceIndicatorExam
    {
        [Key]
        public int Id { get; set; }

        // 🔹 المنهج المرتبط بالاختبار
        [ForeignKey("Curriculum")]
        public int CurriculumId { get; set; }
        public Curriculum Curriculum { get; set; }

        // 🔹 الدفعة (اختيارية لأن الاختبار ممكن يرسل لأكثر من دفعة)
        [ForeignKey("Batch")]
        public int? BatchId { get; set; }
        public Batch Batch { get; set; }

        // 🔹 عنوان الاختبار
        [Required, StringLength(150)]
        public string Title { get; set; }

        // 🔹 الإعدادات العامة
        public int QuestionsPerIndicator { get; set; } = 12;

        // المدة بالدقائق (تساوي عدد الأسئلة أو تُحدد يدويًا)
        public int DurationMinutes { get; set; } = 12;

        // نسبة النجاح المطلوبة
        public double PassPercent { get; set; } = 50;

        // تاريخ الإنشاء
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // 🔹 نوع الاختبار
        public bool IsOnline { get; set; } = true;

        // 🔹 وقت البداية والنهاية
        public DateTime StartAt { get; set; } = DateTime.Now;

        public DateTime EndAt { get; set; } = DateTime.Now.AddDays(7);

        // 🔹 الكود المرجعي (6 أرقام)
        [StringLength(20)]
        public string ReferenceCode { get; set; }

        // 🔹 علاقات الربط مع الجداول الوسيطة
        public ICollection<PerformanceIndicatorExamSection> Sections { get; set; } = new List<PerformanceIndicatorExamSection>();
        public ICollection<PerformanceIndicatorExamToBatch> ExamToBatches { get; set; } = new List<PerformanceIndicatorExamToBatch>();
        public ICollection<PerformanceIndicatorExamStudent> ExamStudents { get; set; } = new List<PerformanceIndicatorExamStudent>();

        public int TotalQuestions { get; set; } = 0;

        public bool IsSent { get; set; } = false; // ✅ يوضح هل تم إرسال الاختبار أم لا

        // 🔹 الأرشفة
        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedByUserId { get; set; }

    }
}
