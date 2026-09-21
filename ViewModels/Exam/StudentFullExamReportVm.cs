namespace QdratNew.ViewModels.Exam
{
    public class StudentFullExamReportVm
    {    // الطالب
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string ParentName { get; set; }
        public string ParentPhone { get; set; }

        // الاختبار
        public string ExamTitle { get; set; }
        public string CurriculumTitle { get; set; }
        public string BatchName { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public double ScorePercent { get; set; }

        // الحضور
        public List<AttendanceVm> AttendanceRecords { get; set; } = new();

        // الواجبات
        public List<HomeworkPerformanceVm> HomeworkPerformances { get; set; } = new();
    }

    public class AttendanceVm
    {
        public string LectureTitle { get; set; }
        public DateTime RecordedAt { get; set; }
        public bool IsPresent { get; set; }
        public string Notes { get; set; }
        public DateTime Date => RecordedAt.Date;
    }

    public class HomeworkPerformanceVm
    {
        public string HomeworkTitle { get; set; }
        public double? Score { get; set; }
        public bool IsSubmitted { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }
}
