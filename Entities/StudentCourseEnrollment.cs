using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class StudentCourseEnrollment
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public Student Student { get; set; }

        public int CourseId { get; set; }
        public Course Course { get; set; }

        public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;
        public bool IsCompleted { get; set; } = false;
        public float? Grade { get; set; }
    }

}
