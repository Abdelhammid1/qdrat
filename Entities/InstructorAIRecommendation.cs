using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class InstructorAIRecommendation
    {
        public int Id { get; set; }

        [Required]
        public int InstructorId { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("InstructorId")]
        public Instructor Instructor { get; set; }
    }
}
