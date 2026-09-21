using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.ViewModels.Exam
{
    public class ExamCreateViewModel
    {
        [Display(Name = "عنوان الاختبار")]
        public string Title { get; set; }

        [Display(Name = "نوع الاختبار")]
        public ExamType Type { get; set; }

        [Display(Name = "المنهج")]
        public int? CurriculumId { get; set; }

        [Display(Name = "الدورة")]
        public int? CourseId { get; set; }

        [Display(Name = "المحاضرة / المؤشر")]
        public int? LessonId { get; set; }

        [Display(Name = "عدد الأسئلة")]
        [Range(1, 100)]
        public int TotalQuestions { get; set; } = 10;

        [Display(Name = "عدد الأسئلة السهلة")]
        public int EasyQuestionCount { get; set; } = 3;

        [Display(Name = "عدد الأسئلة المتوسطة")]
        public int MediumQuestionCount { get; set; } = 4;

        [Display(Name = "عدد الأسئلة الصعبة")]
        public int HardQuestionCount { get; set; } = 3;

        [Display(Name = "المدة الزمنية (بالدقائق)")]
        [Range(1, 300)]
        public int DurationMinutes { get; set; } = 30;

        // ✅ القوائم المنسدلة
        public List<SelectListItem> CurriculumList { get; set; } = new();
        public List<SelectListItem> CourseList { get; set; } = new();
        public List<SelectListItem> LessonList { get; set; } = new();
    }
}
