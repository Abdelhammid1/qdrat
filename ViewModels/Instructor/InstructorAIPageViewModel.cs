namespace QdratNew.ViewModels.Instructor
{
    public class InstructorAIPageViewModel
    {
        public string InstructorName { get; set; } = "";
        public string OverallAIRecommendation { get; set; } = "";
        public string RuleBasedRecommendation { get; set; } = "";

        public int TotalStudents { get; set; }
        public int TotalBatches { get; set; }
        public float AvgAttendanceRate { get; set; }
        public float AvgHomeworkCompletionRate { get; set; }
        public float AvgExamScore { get; set; }
        public int TotalAtRiskStudents { get; set; }

        public List<BatchAIInsightVM> BatchInsights { get; set; } = new();
        public List<AtRiskStudentItemVM> GlobalAtRiskStudents { get; set; } = new();
    }

    public class BatchAIInsightVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseName { get; set; } = "";
        public int StudentCount { get; set; }
        public float AvgAttendance { get; set; }
        public float HomeworkCompletionRate { get; set; }
        public float AvgExamScore { get; set; }
        public float LessonsCompletionRate { get; set; }
        public int AtRiskCount { get; set; }
        public string AIRecommendation { get; set; } = "";
        public string PerformanceLevel { get; set; } = "";
        public string PerformanceTone { get; set; } = "secondary";
        public List<AtRiskStudentItemVM> AtRiskStudents { get; set; } = new();
    }

    public class AtRiskStudentItemVM
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = "";
        public float AvgScore { get; set; }
        public string BatchName { get; set; } = "";
        public int ExamCount { get; set; }
    }
}
