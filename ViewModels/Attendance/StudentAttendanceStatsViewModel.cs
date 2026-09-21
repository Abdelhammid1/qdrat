namespace QdratNew.ViewModels.Attendance
{
    public class StudentAttendanceStatsViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string BranchName { get; set; }

        public int TotalLectures { get; set; }
        public int AttendedLectures { get; set; }
        public int AbsentLectures => TotalLectures - AttendedLectures;

        public double AttendancePercentage =>
            TotalLectures == 0 ? 0 : Math.Round((double)AttendedLectures / TotalLectures * 100, 2);




        public string BatchName { get; set; }
        public string CourseName { get; set; }
        public int BatchId { get; set; }
        
        public string LastLectureAttended { get; set; } = "—";
    
        public double LastFiveAverage { get; set; }
        public int ConsecutiveAbsences { get; set; }

        public List<StudentLectureAttendanceViewModel> Lectures { get; set; } = new();

    }

    public class StudentLectureAttendanceViewModel
    {
        public int LectureId { get; set; }
        public string LectureTitle { get; set; }
        public string InstructorName { get; set; }
        public DateTime Date { get; set; }
        public bool IsPresent { get; set; }
        public string? Notes { get; set; }
    }




}
