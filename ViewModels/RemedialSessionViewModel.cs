using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;

namespace QdratNew.ViewModels
{
    public class RemedialSessionViewModel
    {
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }
        public string StudentName { get; set; }

        [Required]
        public int RemedialPlanId { get; set; }

        [Required]
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }

        [Required(ErrorMessage = "يرجى تحديد التاريخ")]
        public DateTime ScheduledDate { get; set; }

        [Required(ErrorMessage = "يرجى تحديد وقت البدء")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "يرجى تحديد وقت الانتهاء")]
        public TimeSpan EndTime { get; set; }

        [Required(ErrorMessage = "يرجى إدخال اسم الغرفة")]
        public string RoomName { get; set; }

        public int? InstructorId { get; set; }
        public string? InstructorName { get; set; }

        [Required]
        [Range(1, 100)]
        public int TotalSeats { get; set; }

        public int ReservedSeats { get; set; } = 0;

        public bool IsMandatory { get; set; } = true;
        public bool IsConfirmed { get; set; } = false;
        public bool IsCompleted { get; set; } = false;

        [Display(Name = "المصادر التعليمية")]
        public string Resources { get; set; }

        [DataType(DataType.Currency)]
        public decimal? Fee { get; set; }

        [Required]
        public GenderType Gender { get; set; }
        public List<SelectListItem> GenderOptions { get; set; } = new();

        // ✅ القوائم المنسدلة
        public List<SelectListItem> SectionList { get; set; } = new();
        public List<SelectListItem> InstructorList { get; set; } = new();
        public List<SelectListItem> RoomList { get; set; } = new();
        public int SessionId { get; set; }
          public DateTime SessionDate { get; set; }
        public string SessionTime { get; set; }


    }
}
