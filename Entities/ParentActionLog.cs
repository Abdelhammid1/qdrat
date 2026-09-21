using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class ParentActionLog
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Parent")]
        public int ParentId { get; set; }
        public Parent Parent { get; set; } = null!;

        public int? StudentId { get; set; }
        public Student? Student { get; set; }

        [Required, StringLength(100)]
        public string ActionType { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
