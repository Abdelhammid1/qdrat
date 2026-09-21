using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Exam
{
    public class PlacementExamAdvancedVm
    {
        [Display(Name = "الطالب")]
        public int? StudentId { get; set; }

        [Display(Name = "الدفعة")]
        public int? BatchId { get; set; }

        [Required(ErrorMessage = "يجب اختيار الدورة")]
        [Display(Name = "الدورة")]
        public int CourseId { get; set; }

        [Required]
        [Display(Name = "عدد الأسئلة")]
        [Range(10, 200, ErrorMessage = "عدد الأسئلة يجب أن يكون بين 10 و200")]
        public int TotalQuestions { get; set; } = 60;

        [Required]
        [Display(Name = "مدة الاختبار (بالدقائق)")]
        [Range(10, 180, ErrorMessage = "المدة يجب أن تكون بين 10 و180 دقيقة")]
        public int DurationMinutes { get; set; } = 60;

        [Required]
        [Display(Name = "نسبة النجاح (%)")]
        [Range(10, 100)]
        public int PassingScore { get; set; } = 60;

        [Display(Name = "عرض الأسئلة بترتيب عشوائي؟")]
        public bool IsRandomized { get; set; } = true;

        [Display(Name = "عرض اسم المحور على كل سؤال؟")]
        public bool ShowSectionName { get; set; } = false;

        public List<SelectListItem> Students { get; set; } = new();
        public List<SelectListItem> Batches { get; set; } = new();
        public List<SelectListItem> Courses { get; set; } = new();
    }
}
