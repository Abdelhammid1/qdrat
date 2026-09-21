using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudentWeakness
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public Student Student { get; set; }

        [ForeignKey("Lesson")]
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }

        public int MistakeCount { get; set; } = 0;

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public bool IsResolved { get; set; } = false; // تم علاج الضعف


        // متى تم اكتشاف نقطة الضعف
        public DateTime FirstDetectedAt { get; set; } = DateTime.UtcNow;

        // مصدر اكتشاف نقطة الضعف: Homework أو Exam أو AI
        [MaxLength(50)]
        public string WeaknessSource { get; set; } = "Unknown";

        // هل تم عرض هذه الضعف بالفعل في تقرير للطالب؟
        public bool IsNotified { get; set; } = false;

        // عدد المرات التي تكرر فيها الخطأ في هذا الدرس
        public int OccurrenceCount { get; set; } = 1;



    }
}
