using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Partner
{
    public class AddStudentToBatchViewModel
    {
        public int StudentId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        public int BatchId { get; set; }

        public List<SelectListItem> Courses { get; set; } = new();
        public List<SelectListItem> Batches { get; set; } = new();
    }

}
