using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    public enum NotificationCategory
    {
        [Display(Name = "عام")]
        General = 0,

        [Display(Name = "الواجبات")]
        Homework = 1,

        [Display(Name = "الاختبارات")]
        Exam = 2,

        [Display(Name = "الموجه الذكي")]
        Mentor = 3,

        [Display(Name = "الخطة العلاجية")]
        Remedial = 4,

        [Display(Name = "تنبيه مهم")]
        Important = 5,
        [Display(Name = "تذكير")]
        Reminder = 6
    }

}
