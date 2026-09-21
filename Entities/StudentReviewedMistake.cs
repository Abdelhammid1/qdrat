using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudentReviewedMistake
    {
        public int Id { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public Student Student { get; set; }

        public Guid QuestionId { get; set; }

        public DateTime ReviewedAt { get; set; } = DateTime.Now;
    }
}
