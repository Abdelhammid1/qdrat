using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudentRankHistory
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public Student Student { get; set; }

        [ForeignKey("Batch")]
        public int BatchId { get; set; }
        public Batch Batch { get; set; }

        public int Rank { get; set; }
        public int TotalStudents { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.Now;
    }
}
