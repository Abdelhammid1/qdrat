using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace QdratNew.ViewModels.Section
{
    public class SectionUnitAssignmentViewModel
    {
        [Required(ErrorMessage = "الرجاء اختيار المحور")]
        public int SectionId { get; set; }

        public List<int> SelectedUnitIds { get; set; } = new List<int>();

        [ValidateNever] // ✅ هذا هو الحل الحقيقي
        public IEnumerable<SelectListItem> Sections { get; set; }

        [ValidateNever] // ✅ هذا هو الحل الحقيقي
        public IEnumerable<SelectListItem> Units { get; set; }
    }
}
