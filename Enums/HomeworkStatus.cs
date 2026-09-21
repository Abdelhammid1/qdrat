using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    public enum HomeworkStatus
    {
        [Display(Name = "قيد الانتظار")]
        Pending = 0,

        [Display(Name = "تم الحل")]
        Submitted = 1,

        [Display(Name = "تمت المراجعة")]
        Reviewed = 2
    }

}
