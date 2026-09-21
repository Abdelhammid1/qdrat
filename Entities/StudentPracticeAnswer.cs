using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudentPracticeAnswer
    {
        public int Id { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public Student Student { get; set; }

        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        public string SelectedAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsReviewed { get; set; } = false;

        public DateTime AnsweredAt { get; set; } = DateTime.Now;
    }
}
