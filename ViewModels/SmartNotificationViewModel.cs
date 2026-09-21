// المسار: Areas/Admin/ViewModels/SmartNotificationViewModel.cs
namespace QdratNew.Areas.Admin.ViewModels
{
    public class SmartNotificationViewModel
    {
        public int StudentsWithUnsolvedLastHomework { get; set; }
        public int StudentsLateTwoHomeworks { get; set; }
        public int StudentsDidNotStartRemedialPlan { get; set; }
        public int StudentsDidNotSolveThisWeek { get; set; }
    }
}
