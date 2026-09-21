using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    // Sprint 14 (MSE-H / H2): شاشة اختيار الطالب لعرض نتيجته (Drill-down) — تعرض كل طالب مُسنَد له الاختبار
    // فعليًا (مباشرة أو عبر دفعة) مع حالته الحالية (لم يبدأ / قيد التقدّم / مكتمل).
    public class MinistrySimExamSelectStudentVm
    {
        public int ExamId { get; set; }
        public string Title { get; set; }
        public string CourseName { get; set; }
        public List<MinistrySimExamStudentResultRowVm> Students { get; set; } = new();
    }

    public class MinistrySimExamStudentResultRowVm
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public bool HasAttempt { get; set; }
        public bool IsCompleted { get; set; }
        public double? TotalScorePercent { get; set; }
    }
}
