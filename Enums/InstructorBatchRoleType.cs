using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{

    public enum InstructorBatchRoleType
    {
        [Display(Name = "تدريس")]
        Teaching = 1,

        [Display(Name = "إشراف")]
        Supervisor = 2,

        [Display(Name = "الحضور والانصراف")]
        Attendance = 3,

        [Display(Name = "التقارير")]
        Reports = 4
    }
}
