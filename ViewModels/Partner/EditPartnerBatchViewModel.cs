using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Partner
{
    public class EditPartnerBatchViewModel
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public int CourseId { get; set; }

        public List<SelectListItem> Courses { get; set; } = new();
    }

}
