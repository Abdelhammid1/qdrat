using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class BatchLessonCompletion
    {
        public int Id { get; set; }

        [Required]
        public int BatchId { get; set; }
        public Batch Batch { get; set; }

        [Required]
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }

        public DateTime CompletionDate { get; set; } = DateTime.Now;

        // ⛔ لاحقًا هنضيف خصائص مثل:
        // ✅ تم توليد الواجب تلقائيًا؟
        public bool IsHomeworkGenerated { get; set; } = false;

        // ✅ الجديد لدعم التحليل والتحقق
        public DateTime? LastCompletedAt { get; set; } // تاريخ آخر تسجيل
        public string? AddedBy { get; set; } // Admin أو Instructor + اسمه

        [Required(ErrorMessage = "يرجى إدخال عنوان تسجيل المؤشرات")]
        public string CompletionTitle { get; set; }

        public int? SectionId { get; set; }
        public Section? Section { get; set; }

        public int? LectureId { get; set; }
        public Lecture? Lecture { get; set; }

    }
}
