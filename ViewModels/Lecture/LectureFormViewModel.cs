using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using QdratNew.Entities;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Lecture
{
    public class LectureFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "العنوان مطلوب")]
        public string Title { get; set; }

        public string Location { get; set; }

        [Required(ErrorMessage = "تاريخ المحاضرة مطلوب")]
        public DateTime Date { get; set; }

        public TimeSpan? ScheduledTime { get; set; }

        public int? DurationMinutes { get; set; }

        public TimeSpan? ScheduledEndTime { get; set; }

        [Required(ErrorMessage = "يجب اختيار المدرب")]
      
        public int InstructorId { get; set; }

        [Required(ErrorMessage = "يجب اختيار القسم")]
  
        public int SectionId { get; set; }

        [Required(ErrorMessage = "يجب اختيار الكورس")]
   
        public int CourseId { get; set; }

        public List<QdratNew.Entities.Course> Courses { get; set; } = new();

        // 🔹 المدربون المرتبطون فعليًا بمنهج/دفعة المحاضرة عبر InstructorCurriculumBatch (القائمة الافتراضية)
        public List<QdratNew.Entities.Instructor> Instructors { get; set; } = new();

        // 🔹 كل المدربين النشطين — تُستخدم فقط عند تفعيل "تعيين يدوي" من الواجهة
        public List<QdratNew.Entities.Instructor> AllActiveInstructors { get; set; } = new();

        // 🔹 هل تم اختيار مدرب خارج قائمة الربط الرسمية؟ (Checkbox واجهة فقط)
        public bool ManualOverride { get; set; }

        public List<QdratNew.Entities.Section> Sections { get; set; } = new();

        [Required(ErrorMessage = "يجب اختيار الدفعة")]
        public int? BatchId { get; set; }

        [ValidateNever]
        public List<QdratNew.Entities.Batch> Batches { get; set; }





    }
}
