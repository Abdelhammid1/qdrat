using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities.Frontend
{
    public class SubCourse
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; }

        [MaxLength(200)]
        public string Slug { get; set; }

        public string? ShortDescription { get; set; }
        public string? FullDescription { get; set; }
        public string? ImagePath { get; set; }

        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; } = 0;

        // 🔗 الدورة الرئيسية
        [ForeignKey(nameof(FrontendCourse))]
        public int FrontendCourseId { get; set; }
        public FrontendCourse FrontendCourse { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;



      

        // 🔹 هل تظهر في الرئيسية
        public bool ShowOnHomePage { get; set; } = false;

 





    }
}
