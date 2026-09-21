using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class Curriculum
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; }

        [Required, StringLength(1000)]
        public string Description { get; set; }

        //[Required]
        //public int CourseId { get; set; }
        //public Course Course { get; set; }


        public ICollection<CourseCurriculum> CourseCurriculums { get; set; } = new List<CourseCurriculum>();



        public ICollection<CurriculumInstructor> CurriculumInstructors { get; set; } = new List<CurriculumInstructor>();
        public ICollection<Section> Sections { get; set; } = new List<Section>();
        public ICollection<CurriculumModule> Modules { get; set; } = new List<CurriculumModule>();
        public ICollection<StudentPerformance> StudentPerformances { get; set; } = new List<StudentPerformance>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsQuantitative { get; set; }

        [Required, MaxLength(50)]
        public string CurriculumTypeName { get; set; }


        // =========================================================
        // ✅ NEW FIELD: التحكم في اتجاه العرض (RTL / LTR)
        // =========================================================
        public bool IsRTL { get; set; } = true; // افتراضي عربي

    }
}
