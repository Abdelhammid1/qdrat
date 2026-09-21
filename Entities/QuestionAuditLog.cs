using QdratNew.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class QuestionAuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid QuestionId { get; set; }

        [ForeignKey(nameof(QuestionId))]
        public Question Question { get; set; }

        [Required]
        [MaxLength(100)]
        public string Action { get; set; } // مثل: إنشاء، تعديل، مراجعة، حذف

        [MaxLength(100)]
        public string? PerformedByUserId { get; set; } // معرّف المستخدم الذي نفذ العملية

        [Required]
        public DateTime PerformedAt { get; set; } = DateTime.Now;

        [MaxLength(1000)]
        public string? ChangedFieldsSummary { get; set; } // وصف مختصر للتغييرات التي تمت
        public UserRoleType PerformedByRole { get; set; }

        public string PerformedByName { get; set; }

    }
}
