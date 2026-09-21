using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class ExamDraft
    {
        [Key]
        public int Id { get; set; }

        // =========================
        // بيانات المسودة
        // =========================

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        public int CurriculumId { get; set; }

        // الشريك الذي أنشأ المسودة
        [Required]
        public int PartnerId { get; set; }

        public DateTime CreatedAt { get; set; }

        // في حال أرشفة المسودة بدل حذفها
        public bool IsArchived { get; set; }

        // =========================
        // Navigation Properties
        // =========================

        [ForeignKey(nameof(CurriculumId))]
        public Curriculum Curriculum { get; set; }


        public int? CreatedByInstructorId { get; set; }

        public ICollection<ExamDraftQuestion> DraftQuestions { get; set; }
            = new List<ExamDraftQuestion>();
    }
}
