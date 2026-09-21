// المسار: ViewModels/BatchViewModel.cs
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Batch
{
    public class BatchViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الدفعة مطلوب")]
        [Display(Name = "اسم الدفعة")]
        public string Name { get; set; }

        [Required(ErrorMessage = "تاريخ البداية مطلوب")]
        [Display(Name = "تاريخ البداية")]
        public DateTime StartDate { get; set; }

        [Display(Name = "تاريخ النهاية")]
        public DateTime? EndDate { get; set; }

        [Required(ErrorMessage = "يرجى اختيار الدورة المرتبطة")]
        [Display(Name = "الدورة")]
        public int CourseId { get; set; }
        [Required(ErrorMessage = "يرجى اختيار جنس الدفعة")]
        public BatchGender Gender { get; set; }  // ✅ النوع

        [Required]
        public int BranchId { get; set; } // ✅ الجديد

        public bool IsArchived { get; set; }
        public int StudentsCount { get; set; }
        public int LecturesCount { get; set; }

        [Display(Name = "إنشاء المحاضرات تلقائيًا")]
        public bool AutoGenerateLectures { get; set; }

        [Display(Name = "تاريخ ووقت بداية أول محاضرة")]
        public DateTime? FirstLectureStartDateTime { get; set; }

        [Display(Name = "مدة كل محاضرة (بالدقائق)")]
        public int? LectureDurationMinutes { get; set; }

        [ValidateNever] // ✅ تجاهل التحقق لهذا الحقل
       public IEnumerable<SelectListItem> Courses { get; set; }

        [ValidateNever] // ✅ تجاهل التحقق لهذا الحقل
        public IEnumerable<SelectListItem> Branches { get; set; } // ✅ الجديد

    }
}
