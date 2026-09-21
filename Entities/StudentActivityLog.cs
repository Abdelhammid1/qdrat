using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudentActivityLog
    {
        public int Id { get; set; }
        public int ExamAssignmentId { get; set; }
        public int LectureId { get; set; }
        
        public int StudentId { get; set; }
        public Student Student { get; set; }

        public string ActivityType { get; set; } // مثل: Homework, Exam, Video, Lesson
        public string ActivityTitle { get; set; } // اسم الدرس أو الفيديو أو الواجب
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public bool? WasCorrect { get; set; } // فقط إذا كان التفاعل سؤال
        public int? Score { get; set; } // في حالة وجود تقييم رقمي

        public string Source { get; set; } // "Homework", "Mock", "Remedial", "Assignment"...

        public string? Note { get; set; } // ملاحظات تحليلية مستقبلية (استخدام ML)

        public float? ConfidenceScore { get; set; }
        public bool IsPredictedWeakness { get; set; } = false;

        public int? LessonId { get; set; }
        [ForeignKey("LessonId")]
        public Lesson? Lesson { get; set; }

        public int? SectionId { get; set; }
        [ForeignKey("SectionId")]
        public Section? Section { get; set; }

        public Guid? QuestionId { get; set; }

        [ForeignKey("QuestionId")]
        public Question? Question { get; set; }





    }
}
