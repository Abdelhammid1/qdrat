using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Partner
{
    public class CreatePartnerBatchViewModel
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public int CourseId { get; set; }

        public List<SelectListItem> Courses { get; set; } = new();
    }

}
