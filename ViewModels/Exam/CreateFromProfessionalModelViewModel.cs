
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Exam
{
    public class CreateFromProfessionalModelViewModel
    {
        [Display(Name = "المنهج")]
        [Required(ErrorMessage = "يرجى اختيار المنهج")]
        public int SelectedCurriculumId { get; set; }

        [Display(Name = "الدفعة")]
        [Required(ErrorMessage = "يرجى اختيار الدفعة")]
        public int SelectedBatchId { get; set; }

        [Display(Name = "النموذج الاحترافي")]
        [Required(ErrorMessage = "يرجى اختيار النموذج الاحترافي")]
        public int SelectedModelId { get; set; }

        // القوائم المنسدلة
        public List<SelectListItem> AvailableCurriculums { get; set; } = new();
        public List<SelectListItem> AvailableBatches { get; set; } = new();
        public List<SelectListItem> AvailableModels { get; set; } = new();

        // 🟢 حقول الوقت والمدة الجديدة
        public DateTime StartAt { get; set; } = DateTime.Now;
        public DateTime EndAt { get; set; } = DateTime.Now.AddDays(7);
        public int DurationMinutes { get; set; } = 60;
        public string? ErrorMessage { get; set; }
    }
}
