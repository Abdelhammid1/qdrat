using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace QdratNew.ViewModels.Exam
{
    public class ManualExamCreationViewModel
    {
        [Required(ErrorMessage = "يرجى اختيار عنوان")]

        public string Title { get; set; }   // 👈 عنوان مخصص من الإدمن

        [Required(ErrorMessage = "يرجى اختيار الدفعة")]
        public int BatchId { get; set; }

        [Required(ErrorMessage = "يرجى اختيار المنهج")]
        public int CurriculumId { get; set; }

        [Required(ErrorMessage = "يرجى اختيار المحور")]
        public int SectionId { get; set; }

        [Required(ErrorMessage = "يرجى اختيار نوع الاختبار")]
        public ExamType ExamType { get; set; }

        // ✅ هل الاختبار أونلاين؟
        public bool IsOnline { get; set; } = true;

        // ✅ موعد الاختبار (في حال كان حضوري)
        [Display(Name = "موعد الاختبار (في حال كان حضوري)")]
        [DataType(DataType.DateTime)]
        public DateTime? ScheduledDate { get; set; }

        // ✅ هل سيتم تقسيم الاختبار إلى 3 أقسام؟
        public bool EnablePhasedExam { get; set; } = false;

        // ✅ المناهج المستخدمة عند الترتيب الثلاثي
        public int? FirstCurriculumId { get; set; }
        public int? SecondCurriculumId { get; set; }

        // ✅ القوائم الجاهزة للاستخدام في الـ View
        [ValidateNever]
        public List<SelectListItem> Batches { get; set; } = new();

        [ValidateNever]
        public List<SelectListItem> Curriculums { get; set; } = new();

        [ValidateNever]
        public List<SelectListItem> Sections { get; set; } = new();

        [ValidateNever]
        public List<SelectListItem> ExamTypes { get; set; } = new();
        public int? StudentId { get; set; }

        [ValidateNever]
        public List<SelectListItem> Students { get; set; } = new();

        [ValidateNever]
        public string? CourseTitle { get; set; }

        [ValidateNever]
        public List<string> CourseCurriculums { get; set; } = new();

        [Required(ErrorMessage = "يجب اختيار الدورة")]
        public int CourseId { get; set; }
        [ValidateNever]
        public List<SelectListItem> Courses { get; set; } = new();
        public List<CurriculumQuestionCountVm> CurriculumQuestionCounts { get; set; } = new();


        public int DurationMinutes { get; set; }     // مدة الاختبار بالدقائق

        public bool RandomizeQuestions { get; set; } = true;



        public class CurriculumQuestionCountVm
        {
            public int CurriculumId { get; set; }
            public string CurriculumTitle { get; set; }
            public int QuestionCount { get; set; }
        }
        // ✅ مولد داخلي للقائمة المعربة لنوع الاختبار (يمكن استخدامه عند البناء)
        public static List<SelectListItem> GetLocalizedExamTypes()
        {
            return Enum.GetValues(typeof(ExamType))
                .Cast<ExamType>()
                .Select(e => new SelectListItem
                {
                    Value = ((int)e).ToString(),
                    Text = e.GetType()
                            .GetMember(e.ToString())
                            .First()
                            .GetCustomAttribute<DisplayAttribute>()?.Name ?? e.ToString()
                })
                .ToList();
        }
    }
}
