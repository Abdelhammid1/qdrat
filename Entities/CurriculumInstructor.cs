using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class CurriculumInstructor
    {
        public int Id { get; set; }

        public int CurriculumId { get; set; }
        public Curriculum Curriculum { get; set; }

        public int InstructorId { get; set; }
        public Instructor Instructor { get; set; }

        // ✅ استخدام النوع enum بدل string
        public GenderType Gender { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.Now;

        public string? Notes { get; set; }
    }
}
