using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Section
{
    public class SectionViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "عنوان المحور مطلوب")]
        public string Title { get; set; }

        [Required(ErrorMessage = "يرجى اختيار المنهج")]
        [Display(Name = "المنهج")]
        public int CurriculumId { get; set; }

        [BindNever] // ✅ الحل هنا
        public IEnumerable<SelectListItem> Curriculums { get; set; } = new List<SelectListItem>();
    }
}
