using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Students
{
    public class StudySessionReservationViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يرجى اختيار الفرع")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "يرجى اختيار المدرب (أو تركه فارغًا إن لم يكن مطلوبًا)")]
        public int? InstructorId { get; set; }

        [Required(ErrorMessage = "يرجى تحديد تاريخ الجلسة")]
        [DataType(DataType.Date)]
        public DateTime RequestedDate { get; set; }

        [Required(ErrorMessage = "يرجى تحديد وقت البداية")]
        [DataType(DataType.Time)]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "يرجى تحديد وقت الانتهاء")]
        [DataType(DataType.Time)]
        public TimeSpan EndTime { get; set; }

        public string AdditionalServices { get; set; }

        [Range(0, 500, ErrorMessage = "التكلفة يجب أن تكون بين 0 و 500")]
        public decimal? Fee { get; set; }

        [Required(ErrorMessage = "يرجى تحديد عدد المقاعد")]
        [Range(1, 100, ErrorMessage = "عدد المقاعد يجب أن يكون بين 1 و 100")]
        public int TotalSeats { get; set; }
        public int? SuggestedSectionId { get; set; } // 🔁 لعرض التنبيه عند قدوم المحور

        public string? Notes { get; set; }
        public string? RoomName { get; set; }



        // ✅ القوائم المنسدلة
        // ✅ القوائم المنسدلة
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public List<SelectListItem> BranchList { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public List<SelectListItem> InstructorList { get; set; }
    }
}
