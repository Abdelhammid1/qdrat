using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.Helpers
{
    // APD-S5.2: Views نتائج معمل القياس في الأدمن (StudentResult, StudentQuestionReview, BatchAnalytics, ParentReport)
    // تُعرض أيضًا من منطقة المدرسة الشريكة بنفس الملف. هذا يحدد الروابط والـ Layout حسب المنطقة الحالية،
    // فلا تشير صفحة المدرسة لأي رابط في الأدمن.
    public sealed class MinistrySimResultRoutes
    {
        public const string PartnerLayout = "~/Views/Shared/_PartnerLayout.cshtml";

        private MinistrySimResultRoutes(bool isPartner)
        {
            IsPartner = isPartner;
        }

        public bool IsPartner { get; }

        public string Area => IsPartner ? "Partner" : "Admin";
        public string Controller => IsPartner ? "MinistrySimExams" : "MinistrySimExam";

        // قائمة طلاب الاختبار (route: id)
        public string StudentsAction => IsPartner ? "BatchStudents" : "SelectStudentForResult";

        // رجوع من تحليلات الدفعة (route: id) — الأدمن لتفاصيل الاختبار، والمدرسة لطلاب الدفعة
        public string ExamHomeAction => IsPartner ? "BatchStudents" : "Details";

        // PDF عبر Puppeteer للأدمن فقط؛ المدرسة تطبع من المتصفح (لا تُكشف خدمة PDF قبل SRH-R05)
        public bool CanDownloadPdf => !IsPartner;

        public static MinistrySimResultRoutes For(ViewContext viewContext)
            => new(string.Equals(viewContext.RouteData.Values["area"] as string, "Partner", StringComparison.OrdinalIgnoreCase));
    }
}
