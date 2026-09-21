using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    [Flags]
    public enum QuestionUsageType
    {
        [Display(Name = "غير مصنف")]
        None = 0,

        [Display(Name = "واجبات")]
        Assignment = 1,

        [Display(Name = "مهارات تعزيزية")]
        Enhancement = 2,

        [Display(Name = "اختبار الدورة")]
        QdratExam = 4,

        [Display(Name = "محاكاة اختبار الوزارة")]
        OfficialMockExam = 8,

        [Display(Name = "اختبار مقياس مؤشرات الأداء")]
        PlacementTest = 16,
        
        [Display(Name = "اختبار مقياس المستوي")]
        PerformanceScale = 17,

        [Display(Name = "اختبار ترويجي")]
        PromoTest = 32
    }
}
