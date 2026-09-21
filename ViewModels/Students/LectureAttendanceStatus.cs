namespace QdratNew.ViewModels.Students
{
    public class LectureAttendanceStatus
    {
        public string LectureTitle { get; set; }
        public DateTime Date { get; set; }
        public string Section { get; set; }
        public string Course { get; set; }
        public bool IsPresent { get; set; }
    }
}
