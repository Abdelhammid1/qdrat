// ViewModels/Admin/Lecture/LectureEditViewModel.cs
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Lecture
{
    public class LectureEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "العنوان مطلوب")]
        public string Title { get; set; }

        [Required(ErrorMessage = "التاريخ مطلوب")]
        public DateTime Date { get; set; }

        public TimeSpan? ScheduledTime { get; set; }

        public int? DurationMinutes { get; set; }

        public TimeSpan? ScheduledEndTime { get; set; }

        public string Location { get; set; }

   
        public int CourseId { get; set; }


        public int InstructorId { get; set; }

     
        public int SectionId { get; set; }

        [Required(ErrorMessage = "يجب اختيار الدفعة")]
        public int? BatchId { get; set; }

        [ValidateNever]
        public List<SelectListItem> Batches { get; set; }


        // 🔹 هل تم اختيار مدرب خارج قائمة الربط الرسمية؟ (Checkbox واجهة فقط)
        public bool ManualOverride { get; set; }

        [ValidateNever]
        public List<SelectListItem> Courses { get; set; } = new();

        // 🔹 المدربون المرتبطون فعليًا بمنهج/دفعة المحاضرة عبر InstructorCurriculumBatch (القائمة الافتراضية)
        [ValidateNever]
        public List<SelectListItem> Instructors { get; set; } = new();

        // 🔹 كل المدربين النشطين — تُستخدم فقط عند تفعيل "تعيين يدوي" من الواجهة
        [ValidateNever]
        public List<SelectListItem> AllActiveInstructors { get; set; } = new();

        [ValidateNever]
        public List<SelectListItem> Sections { get; set; } = new();


    }
}
