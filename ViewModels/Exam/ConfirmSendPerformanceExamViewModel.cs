namespace QdratNew.ViewModels.Exam
{
    public class ConfirmSendPerformanceExamViewModel
    {
        // 🟢 المعرّف الرئيسي للاختبار
        public int ExamId { get; set; }

        // 🟢 عنوان الاختبار
        public string ExamTitle { get; set; } = string.Empty;

        // 🟢 اسم المنهج المرتبط بالاختبار
        public string CurriculumTitle { get; set; } = string.Empty;

        // 🟢 اسم الدفعة التي سيُرسل إليها
        public string BatchName { get; set; } = string.Empty;

        // 🟢 الكود المرجعي الفريد
        public string ReferenceCode { get; set; } = string.Empty;

        // 🟢 عدد الطلاب الذين سيُرسل إليهم الاختبار
        public int StudentCount { get; set; }

        // APD-S4.1: قالب إرسال للشركاء: يُختار له المدارس ودفعاتها هنا
        public bool IsPartnerTemplate { get; set; }
        public QdratNew.ViewModels.PartnerDispatch.DispatchAudiencePickerVm? AudiencePicker { get; set; }
    }
}
