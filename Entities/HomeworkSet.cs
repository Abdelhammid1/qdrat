using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using QdratNew.Entities;


namespace QdratNew.Entities
{
    public class HomeworkSet
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "عنوان الواجب")]
        public string Title { get; set; } = "";

        [Required]
        public int BatchId { get; set; }
        public Batch Batch { get; set; }

        public int? CurriculumId { get; set; }
        public Curriculum? Curriculum { get; set; }

        public int? LectureId { get; set; }
        public Lecture? Lecture { get; set; }

        [Display(Name = "تاريخ الإرسال")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "مرسل بواسطة")]
        public string? AssignedByUserId { get; set; }
        public ApplicationUser? AssignedByUser { get; set; }
        public string CompletionTitle { get; set; } // ⬅️ هذا هو عنوان المؤشرات المرتبطة بالواجب
        public bool IsSent { get; set; } = false;

        public ICollection<Homework> Homeworks { get; set; } = new List<Homework>();



        // === NEW FIELDS (HomeworkSet) ===
        public bool IsExtra { get; set; } = false;          // واجب إضافي
        public DateTime? StartAt { get; set; }              // نافذة الحل - بداية
        public DateTime? EndAt { get; set; }                // نافذة الحل - نهاية
        public bool AllowRetake { get; set; } = true;       // السماح بإعادة المحاولة
        public int MaxRetakes { get; set; } = 2;            // عدد المحاولات المسموح بها
        public bool KeepSameQuestionsOnRetake { get; set; } = true; // نفس الأسئلة في الإعادة؟

        // === Navigation Properties للجداول الجديدة ===
        public ICollection<HomeworkSetAttempt> Attempts { get; set; } = new List<HomeworkSetAttempt>();
        public ICollection<HomeworkSetSection> Sections { get; set; } = new List<HomeworkSetSection>();
        public ICollection<HomeworkSetStudent> Students { get; set; } = new List<HomeworkSetStudent>();
        public bool IsClosed { get; set; } = false;

        public bool IsFromProfessionalModel { get; set; } = false;

        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedByUserId { get; set; }

        public ICollection<HomeworkArchiveAccess> ArchiveAccesses { get; set; } = new List<HomeworkArchiveAccess>();



    }
}
