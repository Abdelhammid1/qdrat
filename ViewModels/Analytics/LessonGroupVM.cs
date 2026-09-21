using QdratNew.ViewModels.Admin.Analytics;

namespace QdratNew.ViewModels.Analytics
{
    public class LessonGroupVM
    {
        public string GroupName { get; set; }
        public List<LessonAnalyticsVM> Lessons { get; set; }
    }
}
