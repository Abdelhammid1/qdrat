namespace QdratNew.ViewModels.Admin.Analytics
{
    public class BatchPerformanceDetailsVM
    {
        public int BatchId { get; set; }
        public int? CurriculumId { get; set; }
        public int? InstructorId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseName { get; set; } = "";

        public int StudentsCount { get; set; }
        public int TotalAttempts { get; set; }
        public int CorrectAttempts { get; set; }
        public int WrongAttempts { get; set; }

        public double AvgScore { get; set; }
        public double WeaknessPercentage { get; set; }

        public int CriticalLessonsCount { get; set; }
        public int HighRiskQuestionsCount { get; set; }

        public string RiskLevel { get; set; } = "";
        public string DecisionMessage { get; set; } = "";

        public List<BatchInstructorSummaryVM> Instructors { get; set; } = new();
        public List<BatchWeakLessonDetailsVM> WeakLessons { get; set; } = new();
        public List<BatchStudentRiskVM> Students { get; set; } = new();
    }

    public class BatchInstructorSummaryVM
    {
        public int InstructorId { get; set; }
        public string InstructorName { get; set; } = "";
        public int WeakLessonsCount { get; set; }
        public int HighRiskQuestionsCount { get; set; }
        public double AvgScore { get; set; }
    }

    public class BatchWeakLessonDetailsVM
    {
        public int LessonId { get; set; }
        public string LessonName { get; set; } = "";

        public int? InstructorId { get; set; }
        public string InstructorName { get; set; } = "";

        public int AffectedStudents { get; set; }
        public int TotalAttempts { get; set; }
        public int WrongAttempts { get; set; }
        public double WeaknessPercentage { get; set; }

        public List<BatchHighRiskQuestionVM> HighRiskQuestions { get; set; } = new();
    }

    public class BatchHighRiskQuestionVM
    {
        public Guid QuestionId { get; set; }
        public string ReferenceNumber { get; set; } = "";
        public string QuestionTitle { get; set; } = "";

        public int TotalAttempts { get; set; }
        public int WrongAttempts { get; set; }
        public int CorrectAttempts { get; set; }
        public double ErrorPercentage { get; set; }
    }

    public class BatchStudentRiskVM
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = "";

        public double ExamScore { get; set; }
        public double HomeworkScore { get; set; }
        public double Attendance { get; set; }

        public string RiskLevel { get; set; } = "";
    }
}
