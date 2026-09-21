using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class InstructorModel
    {
        public int Id { get; set; }

        public int InstructorId { get; set; }
        public Instructor Instructor { get; set; }

        public int CurriculumId { get; set; }
        public Curriculum Curriculum { get; set; }

        [Required]
        public string Title { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<InstructorModelQuestion> Questions { get; set; }
    }
}