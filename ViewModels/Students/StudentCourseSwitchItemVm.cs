namespace QdratNew.ViewModels.Students
{
    public class StudentCourseSwitchItemVm
    {
        public int CourseId { get; set; }
        public int BatchId { get; set; }

        public string CourseTitle { get; set; } = "";
        public string BatchTitle { get; set; } = "";

        public bool IsActive { get; set; }
    }
}
