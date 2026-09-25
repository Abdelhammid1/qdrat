using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class Course
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }

        public int? ProjectId { get; set; }
        public Project Project { get; set; }

        public int? BranchId { get; set; }
        public Branch Branch { get; set; }

        //public ICollection<Curriculum> Curriculums { get; set; } = new List<Curriculum>();

        public ICollection<CourseCurriculum> CourseCurriculums { get; set; } = new List<CourseCurriculum>();



        public ICollection<CourseInstructor> CourseInstructors { get; set; } = new List<CourseInstructor>();
        
        [InverseProperty(nameof(StudentCourse.Course))]
        public ICollection<StudentCourse> StudentCourses { get; set; }
            = new List<StudentCourse>();
        public ICollection<StudentCourseEnrollment> StudentCourseEnrollments { get; set; } = new List<StudentCourseEnrollment>();

        // ===== صفحة التسجيل العامة (RL) =====
        public bool ShowOnRegisterPage { get; set; } = false;
        public int RegisterDisplayOrder { get; set; } = 0;

        [MaxLength(300)]
        public string? PublicDescription { get; set; }
    }
}
