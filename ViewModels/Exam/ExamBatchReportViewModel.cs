using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    // 📌 ViewModel لعرض تقرير شامل عن اختبار دفعة
    public class ExamBatchReportViewModel
    {
        public int AssignmentId { get; set; }
        public string ExamTitle { get; set; }
        public string BatchName { get; set; }

        public int TotalStudents { get; set; }
        public int CompletedCount { get; set; }
        public int NotCompletedCount { get; set; }
        public double AverageScore { get; set; }

        public string TopStudentName { get; set; }
        public string WeakStudentName { get; set; }

        // 📌 قائمة الطلاب المرتبطين بالاختبار
        public List<ExamStudentViewModel> Students { get; set; } = new();
    }
}
