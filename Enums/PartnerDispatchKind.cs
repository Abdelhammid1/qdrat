using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    // APD-S1: نوع ما يرسله الأدمن للشركاء. القيم مخزنة في PartnerDispatchLogs.Kind فلا تُغيَّر.
    public enum PartnerDispatchKind
    {
        [Display(Name = "واجب من نموذج")]
        Homework = 1,

        [Display(Name = "اختبار من نموذج")]
        Exam = 2,

        [Display(Name = "تحديد المستوى من نموذج")]
        Placement = 3,

        [Display(Name = "اختبار مؤشر الأداء")]
        PerformanceIndicator = 4,

        [Display(Name = "معمل القياس")]
        MinistrySim = 5
    }
}
