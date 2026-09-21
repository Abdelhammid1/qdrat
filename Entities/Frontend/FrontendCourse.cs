using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities.Frontend
{
    public class FrontendCourse
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [MaxLength(200)]
        public string Slug { get; set; } // اسم الرابط (مثل qiyas)

        public string ImagePath { get; set; } // صورة الغلاف
        public string ShortDescription { get; set; } // وصف مختصر
        public string FullDescription { get; set; } // محتوى كامل HTML

        public bool ShowInNavbar { get; set; } // هل تظهر في القائمة العلوية؟
        public int DisplayOrder { get; set; } // ترتيب العرض
        public bool IsActive { get; set; } = true;
        public bool ShowAsMainLink { get; set; } // ✅ تظهر كعنصر رئيسي في القائمة وليس في الدورات

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<FrontendCourseSection> Sections { get; set; }




        // 🧩 العلاقة مع الدورات الفرعية
        public ICollection<SubCourse> SubCourses { get; set; } = new List<SubCourse>();


    }
}
