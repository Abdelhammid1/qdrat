using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class ParentMessage
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Parent")]
        public int ParentId { get; set; }
        public Parent Parent { get; set; } = null!;

        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;

        [Required, StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string MessageBody { get; set; } = string.Empty;

        [StringLength(50)]
        public string Status { get; set; } = "New";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? RepliedAt { get; set; }

        public string? RepliedByUserId { get; set; }
        public ApplicationUser? RepliedByUser { get; set; }

        public string? ReplyBody { get; set; }
    }
}
