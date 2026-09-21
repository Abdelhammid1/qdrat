using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Homework
{
    public class CreateModelHomeworkViewModel
    {
        public int BatchId { get; set; }

        [Required]
        public int ModelId { get; set; }

        public int? CurriculumId { get; set; }

        [ValidateNever]
        public List<SelectListItem> Curriculums { get; set; } = new();

        [ValidateNever]
        public List<int> SelectedStudentIds { get; set; } = new List<int>();
        [ValidateNever]
        public List<SelectListItem> Models { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> Students { get; set; }


        public string Title { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        [ValidateNever]
        public List<SelectListItem> Batches { get; set; }

        public List<int> SelectedBatchIds { get; set; } = new();

        [Display(Name = "واجب مرتبط بمحاضرة")]
        public bool IsLinkedToLecture { get; set; }

        [Display(Name = "المحاضرة")]
        public int? LectureId { get; set; }

        [ValidateNever]
        public List<SelectListItem> Lectures { get; set; } = new();




    }
}
