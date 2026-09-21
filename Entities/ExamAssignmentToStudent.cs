using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class ExamAssignmentToStudent
    {
        public int Id { get; set; }

        [Required]
        public int ExamId { get; set; }
        public Exam Exam { get; set; }

        [Required]
        public int StudentId { get; set; }
        public Student Student { get; set; }

        // وقت بداية الامتحان
        [Required(ErrorMessage = "يجب تحديد موعد بداية الاختبار")]
        public DateTime? ScheduledDate { get; set; }

        [Required(ErrorMessage = "يجب تحديد موعد نهاية الاختبار")]
        public DateTime? EndAt { get; set; }

        // المدة الزمنية
        public int DurationMinutes { get; set; }

        public int QuestionCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsOnline { get; set; } = true;
        public bool IsInLab { get; set; } = false;

        public bool IsSent { get; set; } = true;

        // "Official" للاختبارات الرسمية، "ParentRequest" للتدريب من ولي الأمر
        [StringLength(50)]
        public string? SourceType { get; set; } = "Official";
    }
}
