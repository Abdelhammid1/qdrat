using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities.Frontend
{
    public class FrontendCourseSection
    {
        public int Id { get; set; }

        [ForeignKey("FrontendCourse")]
        public int FrontendCourseId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } // عنوان القسم

        public string Content { get; set; } // النص أو HTML
        public string ImagePath { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;

        public virtual FrontendCourse FrontendCourse { get; set; }
    }
}
