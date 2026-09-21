using System.ComponentModel.DataAnnotations;
using QdratNew.Entities;
using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class Assignment
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime DueDate { get; set; }

        public int BatchId { get; set; }
        public Batch Batch { get; set; }

        public int CurriculumId { get; set; }
        public Curriculum Curriculum { get; set; }

        public int SectionId { get; set; }
        public Section Section { get; set; }

        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }

        public ICollection<AssignmentQuestion> Questions { get; set; } = new List<AssignmentQuestion>();
    }

}
