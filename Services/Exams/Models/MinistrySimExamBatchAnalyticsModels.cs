using QdratNew.ViewModels.Exam;

namespace QdratNew.Services.Exams.Models
{
    // Sprint 18 (MSE-K / K1): استخراج منطق BatchAnalytics (Sprint 15/16، MSE-I) من الـController إلى خدمة —
    // بلا أي تغيير في سلوك BatchAnalytics نفسها (نفس الحالات: اختبار غير موجود / الدفعة غير مُسنَد لها الاختبار).
    public enum MinistrySimExamBatchAnalyticsStatus
    {
        Ok,
        ExamNotFound,
        AssignmentNotFound
    }

    public class MinistrySimExamBatchAnalyticsLookup
    {
        public MinistrySimExamBatchAnalyticsStatus Status { get; set; }
        public MinistrySimExamBatchAnalyticsVm Vm { get; set; }
    }

    // Sprint 18 (MSE-K / K1): ملخص متوسطات دفعة خفيف الوزن لتقرير ولي الأمر (ParentReport) — لا يحتاج
    // تفصيل كل طالب/كل مرحلة كما في MinistrySimExamBatchAnalyticsVm الكاملة، لكنه محسوب بنفس قواعد الحساب بالضبط
    // (نفس تعريف "مكتمل"، نفس طريقة حساب متوسط الكمي/اللفظي) عبر نفس المنطق المشترك.
    public class MinistrySimExamBatchAverageStatsVm
    {
        public int TotalStudents { get; set; }
        public double AvgScorePercent { get; set; }
        public double AvgQuantPercent { get; set; }
        public double AvgVerbalPercent { get; set; }
    }
}
