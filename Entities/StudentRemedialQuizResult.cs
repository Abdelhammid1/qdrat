using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudentRemedialQuizResult
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("RemedialQuiz")]
        public int RemedialQuizId { get; set; }
        public RemedialQuiz RemedialQuiz { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public Student Student { get; set; }

        public int Score { get; set; }
        public DateTime TakenAt { get; set; } = DateTime.UtcNow;

        public bool Passed { get; set; }


     


        public bool IsCorrect { get; set; }


    }
}
