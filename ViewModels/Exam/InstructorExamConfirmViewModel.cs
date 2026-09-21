using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    public class InstructorExamConfirmViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }

        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; }

        public string ExamTitle { get; set; }
        public int DurationMinutes { get; set; }
        public DateTime? ScheduledDate { get; set; }

        public List<LessonSummaryForExamViewModel> Lessons { get; set; } = new();
    }

    public class LessonSummaryForExamViewModel
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }

        public int TotalQuestions { get; set; }            // جميع الأسئلة
        public int ReviewedQuestions { get; set; }         // فقط المراجعة
        public int SelectedCount { get; set; }             // عدد الأسئلة المختار لهذا الدرس

        public int SectionId { get; set; }                 // للمطابقة لاحقًا
    }
}
