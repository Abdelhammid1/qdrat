using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudentPrediction
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }

        public float Score { get; set; }

        public DateTime GeneratedAt { get; set; }

        // ✅ الربط بالطالب
        public Student Student { get; set; }
    }
}
