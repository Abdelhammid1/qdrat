using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Instructor
{
    public class InstituteWorkingHoursViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يرجى اختيار اليوم")]
        public DayOfWeek DayOfWeek { get; set; }

        [Required(ErrorMessage = "يرجى إدخال وقت البدء")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "يرجى إدخال وقت الانتهاء")]
        public TimeSpan EndTime { get; set; }

        public TimeSpan? BreakStart { get; set; }

        public TimeSpan? BreakEnd { get; set; }

        public bool IsWorkingDay { get; set; }

        // ✅ لإظهار الأيام باللغة العربية في القائمة المنسدلة
        public List<SelectListItem> AvailableDays { get; set; } = new List<SelectListItem>();

       
    }
}
