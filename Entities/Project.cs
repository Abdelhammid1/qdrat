using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class Project
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المشروع مطلوب.")]
        public required string Name { get; set; } // ✅ سيجبر المطور على تعيين قيمة عند إنشاء `Project`

        public required string Description { get; set; } // ✅ يجب تعيين قيمة عند إنشاء `Project`

        //[Required(ErrorMessage = "يجب تحديد تاريخ البدء.")]
        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        // ✅ ربط المشروع بالفرع مع منع Null
        //[Required(ErrorMessage = "يجب اختيار الفرع التابع له المشروع.")]
        public int BranchId { get; set; }

        [ForeignKey("BranchId")]
        [Required]
        public Branch Branch { get; set; } // ✅ سيجبر المطور على تعيين قيمة عند إنشاء `Project`
                                           // ✅ الإضافة الجديدة المطلوبة
        [Display(Name = "التاريخ المتوقع للانتهاء")]
        [DataType(DataType.Date)]
        public DateTime? ExpectedEndDate { get; set; }


        // ✅ العلاقة مع الدورات
        public ICollection<Course> Courses { get; set; } = new List<Course>();
    }
}
