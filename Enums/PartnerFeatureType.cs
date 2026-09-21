using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    public enum PartnerFeatureType
    {
        [Display(Name = "الواجبات")]
        Homework = 1,

        [Display(Name = "الاختبارات")]
        Exams = 2,

        [Display(Name = "اختبارات تحديد المستوى")]
        PlacementExams = 3,

        [Display(Name = "اختبارات مؤشر الأداء")]
        PerformanceIndicatorExams = 4,

        [Display(Name = "المهارات التعزيزية")]
        ReinforcementSkills = 5,

        [Display(Name = "الخطة العلاجية")]
        RemedialPlans = 6,

        [Display(Name = "الجلسات العلاجية")]
        RemedialSessions = 7,

        [Display(Name = "المحتوى التعليمي")]
        EducationalContent = 8,

        [Display(Name = "تحليلات الذكاء الاصطناعي")]
        AIAnalytics = 9
    }
}
