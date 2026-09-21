using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudySessionRating
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public Student Student { get; set; }

        [ForeignKey("Session")]
        public int SessionId { get; set; }
        public StudySessionReservation Session { get; set; }

        [Range(1, 5)]
        public int TrainerClarity { get; set; } // وضوح الشرح

        [Range(1, 5)]
        public int TrainerCommitment { get; set; } // الالتزام بالمواعيد

        [Range(1, 5)]
        public int SessionBenefit { get; set; } // الاستفادة من الجلسة

        [MaxLength(1000)]
        public string Comment { get; set; }

        public DateTime RatedAt { get; set; } = DateTime.UtcNow;
    }
}
