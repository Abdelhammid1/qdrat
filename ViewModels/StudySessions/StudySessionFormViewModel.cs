using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;

namespace QdratNew.ViewModels.StudySessions
{
    public class StudySessionFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يرجى اختيار الجنس")]
        public GenderType Gender { get; set; }

        public List<SelectListItem> GenderOptions { get; set; } = new();

        [Required(ErrorMessage = "يرجى تحديد التاريخ")]
        [DataType(DataType.Date)]
        public DateTime ScheduledDate { get; set; }

        [Required(ErrorMessage = "يرجى تحديد وقت البدء")]
        [DataType(DataType.Time)]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "يرجى تحديد وقت الانتهاء")]
        [DataType(DataType.Time)]
        public TimeSpan EndTime { get; set; }

        [Range(0, 10000, ErrorMessage = "الرسوم يجب أن تكون بين 0 و 10000")]
        public decimal? Fee { get; set; }

        [Display(Name = "خدمات إضافية")]
        public string? AdditionalServices { get; set; }

        [Display(Name = "اسم المدرّب (اختياري)")]
        public int? InstructorId { get; set; }

        public List<SelectListItem> Instructors { get; set; } = new();

        // مستقبلاً يمكن إضافة قائمة الفروع أو الغرف هنا
    }
}
