using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Instructor.Lectures
{
    public class InstructorLecturesIndexViewModel
    {
        public int? BatchId { get; set; }
        public List<SelectListItem> Batches { get; set; } = new();
        public List<InstructorLectureListItemViewModel> Lectures { get; set; } = new();
    }

    public class InstructorLectureListItemViewModel
    {
        public int LectureId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string SectionTitle { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    public class InstructorLectureFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "عنوان المحاضرة مطلوب")]
        public string Title { get; set; } = string.Empty;

        public string? Location { get; set; }

        [Required(ErrorMessage = "تاريخ المحاضرة مطلوب")]
        public DateTime Date { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "يجب اختيار الدفعة")]
        public int BatchId { get; set; }

        [Required(ErrorMessage = "يجب اختيار المحور")]
        public int SectionId { get; set; }

        public int CourseId { get; set; }

        public List<SelectListItem> Batches { get; set; } = new();
        public List<SelectListItem> Sections { get; set; } = new();
    }
}