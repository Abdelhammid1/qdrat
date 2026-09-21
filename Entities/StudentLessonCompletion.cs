using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class StudentLessonCompletion
    {
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }
        public Student Student { get; set; }

        [Required]
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }

        public DateTime CompletionDate { get; set; } = DateTime.Now;
    }
}
