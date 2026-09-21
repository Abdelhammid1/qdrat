using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Instructor
{
    public class InstructorWorkScheduleViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يرجى اختيار المدرب")]
        public int InstructorId { get; set; }

        // ❌ إزالة التحقق لأن هذا الحقل ليس مطلوبًا من المستخدم
        public string? InstructorName { get; set; } // مخصص للعرض فقط

        [Required(ErrorMessage = "يرجى اختيار اليوم")]
        public DayOfWeek DayOfWeek { get; set; }

        [Required(ErrorMessage = "يرجى إدخال وقت البداية")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "يرجى إدخال وقت النهاية")]
        public TimeSpan EndTime { get; set; }

        public TimeSpan? BreakStart { get; set; }

        public TimeSpan? BreakEnd { get; set; }

        public bool IsWorkingDay { get; set; } = true;
        public bool? IsAvailable { get; set; }

        // ✅ القوائم المنسدلة:
        public List<SelectListItem> Instructors { get; set; } = new();
        public List<SelectListItem> ArabicDays { get; set; } = new();
    }
}
