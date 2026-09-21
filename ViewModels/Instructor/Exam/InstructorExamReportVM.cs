namespace QdratNew.ViewModels.Instructor.Exam
{
    public class InstructorExamReportVM
    {
        public string ExamTitle { get; set; }

        public int TotalStudents { get; set; }
        public int Attempted { get; set; }

        public double AvgScore { get; set; }
        public double HighestScore { get; set; }
        public double LowestScore { get; set; }

        // 🔥 جديد
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }

        public int Range0_50 { get; set; }
        public int Range50_75 { get; set; }
        public int Range75_100 { get; set; }
    }
}
