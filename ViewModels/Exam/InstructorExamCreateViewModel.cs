using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Exam
{
    public class InstructorExamCreateViewModel
    {
        [Required(ErrorMessage = "الرجاء اختيار الدفعة")]
        public int BatchId { get; set; }

        [Required(ErrorMessage = "الرجاء اختيار المنهج")]
        public int CurriculumId { get; set; }

        [Required(ErrorMessage = "الرجاء اختيار المحور")]
        public int SectionId { get; set; }
        [Required(ErrorMessage = "يرجى اختيار المؤشر")]
        public int LessonId { get; set; }

        public List<int> SelectedLessonIds { get; set; } = new List<int>();

        [Required(ErrorMessage = "يرجى إدخال عنوان للاختبار")]
        public string ExamTitle { get; set; }

        [Required(ErrorMessage = "يرجى تحديد عدد الأسئلة")]
        [Range(1, 100, ErrorMessage = "عدد الأسئلة يجب أن يكون بين 1 و 100")]
        public int QuestionCount { get; set; }

        [Required(ErrorMessage = "يرجى تحديد مدة الاختبار")]
        [Range(5, 180, ErrorMessage = "المدة يجب أن تكون بين 5 و 180 دقيقة")]
        public int DurationMinutes { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? ScheduledDate { get; set; }

        // Dropdowns
        public List<SelectListItem> BatchList { get; set; } = new();
        public List<SelectListItem> CurriculumList { get; set; } = new();
        public List<SelectListItem> SectionList { get; set; } = new();
        public List<LessonViewItem> AvailableLessons { get; set; } = new();
        public List<SelectListItem> LessonList { get; set; } = new();


    }

    public class LessonViewItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }
}
