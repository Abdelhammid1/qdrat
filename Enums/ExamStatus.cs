using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    public enum ExamStatus
    {
        [Display(Name = "قيد الانتظار")]
        Pending = 0,

        [Display(Name = "قيد الحل")]
        InProgress = 1,

        [Display(Name = "مكتمل")]
        Completed = 2,

        [Display(Name = "لم يتم الحضور")]
        Missed = 3
    }
}
