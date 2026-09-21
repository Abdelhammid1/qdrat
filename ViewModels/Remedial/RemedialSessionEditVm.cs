using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Remedial
{
    public class RemedialSessionEditVm
    {
        public int Id { get; set; }

        [Display(Name = "الطالب")]
        public string StudentName { get; set; }

        [Display(Name = "الخطة العلاجية")]
        public string PlanTitle { get; set; }

        [Display(Name = "تاريخ ووقت الجلسة")]
        [DataType(DataType.DateTime)]
        public DateTime? ScheduledDate { get; set; }

        [Display(Name = "تم تأكيد الحضور؟")]
        public bool IsConfirmed { get; set; }

        [Display(Name = "ملاحظات إضافية")]
        public string? Notes { get; set; }

    }
}
