using QdratNew.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudentPost
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Student")]
        public int StudentID { get; set; }
        public Student Student { get; set; }

        [Required]
        [MaxLength(500)]
        public string Content { get; set; }

        public string ImagePath { get; set; }
        public DateTime PostedAt { get; set; } = DateTime.Now;

        public int Likes { get; set; } = 0;
        public int Shares { get; set; } = 0;

        [ForeignKey("Batch")]
        public int BatchId { get; set; }
        public Batch Batch { get; set; }
    }
}