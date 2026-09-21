using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    /// <summary>
    /// يربط بين اختبار مؤشر الأداء والطلاب المخصصين له
    /// </summary>
    public class PerformanceIndicatorExamStudent
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("PerformanceIndicatorExam")]
        public int PerformanceIndicatorExamId { get; set; }
        public PerformanceIndicatorExam PerformanceIndicatorExam { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public Student Student { get; set; }
        public DateTime? StartedAt { get; set; }   // 🕒 وقت بدء الطالب للاختبار

        public bool IsCompleted { get; set; } = false;   // هل الطالب أنهى الاختبار
        public double? ScorePercent { get; set; }        // نتيجة الطالب إن وجدت
        public DateTime? CompletedAt { get; set; }       // وقت إنهاء الاختبار
    }
}
