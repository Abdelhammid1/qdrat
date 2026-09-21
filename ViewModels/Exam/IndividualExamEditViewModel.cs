using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Exam
{
    public class IndividualExamEditViewModel
    {
        // رقم Assignment الفعلي
        public int AssignmentId { get; set; }

        // رقم الاختبار الأصلي
        public int ExamId { get; set; }

        // معلومات عامة
        [Required]
        public string Title { get; set; }

        [Required]
        public int StudentId { get; set; }

        public string? StudentName { get; set; }

        [Required]
        public int QuestionCount { get; set; }

        [Required]
        public int DurationMinutes { get; set; }

        // وقت البداية والنهاية
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }

        // للعرض داخل القائمة
        public SelectList? Students { get; set; }
    }
}
