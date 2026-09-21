using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Partner
{
    public class PartnerStudentImportViewModel
    {
        public int CourseId { get; set; }
        public int BatchId { get; set; }

        [ValidateNever]
        public List<SelectListItem> Courses { get; set; } = new();
    }
}
