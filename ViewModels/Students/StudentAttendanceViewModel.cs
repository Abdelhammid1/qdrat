namespace QdratNew.ViewModels.Students
{
    public class StudentAttendanceViewModel
    {
        public string LectureTitle { get; set; }
        public DateTime Date { get; set; }
        public string Course { get; set; }
        public string Section { get; set; }
        public bool IsPresent { get; set; }



        // ✅ إضافات مفيدة:
        public string StatusText => IsPresent ? "حضر ✅" : "غياب ❌";
        public string StatusColor => IsPresent ? "success" : "danger";
        public string FormattedDate => Date.ToString("yyyy/MM/dd");


        public int StudentId { get; set; }
        public int LectureId { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; }
        public DateTime LectureDate { get; set; }




    }
}
