using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    public enum StudySessionStatus
    {
        [Display(Name = "قيد الانتظار")]
        Pending,

        [Display(Name = "معتمدة")]
        Approved,

        [Display(Name = "مرفوضة")]
        Rejected,

        [Display(Name = "ملغاة")]
        Canceled   // ✅ الإضافة الجديدة
    }
}
