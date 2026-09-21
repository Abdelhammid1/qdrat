using System.ComponentModel.DataAnnotations;

public enum StudentActivityType
{
    [Display(Name = "الواجبات")]
    Homework,

    [Display(Name = "الاختبارات")]
    Exam,

    [Display(Name = "الدروس")]
    Lesson,

    [Display(Name = "الفيديوهات")]
    Video,

    [Display(Name = "مشاركات الطالب")]
    Post,

    [Display(Name = "جلسة علاجية")]
    Interaction,

    [Display(Name = "خطة علاجية")]
    Plan,

    [Display(Name = "توصية الذكاء الاصطناعي")]
    Recommendation
}
