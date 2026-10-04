using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    public enum QuestionReviewTaskStatus
    {
        [Display(Name = "مُسندة")]       Assigned   = 1,
        [Display(Name = "قيد المراجعة")] InProgress = 2,
        [Display(Name = "مكتملة")]       Completed  = 3, // لا توجد عناصر Pending
        [Display(Name = "مغلقة")]        Closed     = 4, // أغلقها الأدمن بعد معالجة المرتجعات
        [Display(Name = "ملغاة")]        Cancelled  = 5
    }

    public enum QuestionReviewTaskItemStatus
    {
        [Display(Name = "بانتظار المراجعة")]   Pending           = 0,
        [Display(Name = "اعتمده المدرب")]      Approved          = 1,
        [Display(Name = "عُدِّل واعتُمد")]      EditedAndApproved = 2,
        [Display(Name = "مُرجَع للإدارة")]      Returned          = 3,
        [Display(Name = "اعتمدته الإدارة")]    ApprovedByAdmin   = 4,
        [Display(Name = "أُزيل من المهمة")]     Removed           = 5,
        [Display(Name = "عولج المرتجع")]       ReturnResolved    = 6
    }

    public enum QuestionReviewTaskPriority
    {
        [Display(Name = "عادية")] Normal = 0,
        [Display(Name = "مرتفعة")] High = 1,
        [Display(Name = "عاجلة")] Urgent = 2
    }
}
