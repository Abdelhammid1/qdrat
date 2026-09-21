namespace QdratNew.ViewModels.Admin.Analytics
{
    public class WeakLessonDetailsVM
    {
        public int LessonId { get; set; }
        public int? CurriculumId { get; set; }
        public string LessonName { get; set; } = "";

        public int? BatchId { get; set; }
        public string BatchName { get; set; } = "";

        public int? InstructorId { get; set; }
        public string InstructorName { get; set; } = "";

        public int TotalAttempts { get; set; }
        public int WrongAttempts { get; set; }
        public int CorrectAttempts { get; set; }

        public double WeaknessPercentage { get; set; }
        public int AffectedStudents { get; set; }

        public string RiskLevel { get; set; } = "";
        public string DecisionMessage { get; set; } = "";

        public List<WeakLessonQuestionDetailsVM> Questions { get; set; } = new();
    }

    public class WeakLessonQuestionDetailsVM
    {
        public Guid QuestionId { get; set; }
        public string ReferenceNumber { get; set; } = "";
        public string QuestionTitle { get; set; } = "";

        public int TotalAttempts { get; set; }
        public int WrongAttempts { get; set; }
        public int CorrectAttempts { get; set; }

        public double ErrorPercentage { get; set; }

        public List<WeakLessonStudentErrorVM> WrongStudents { get; set; } = new();
    }

    public class WeakLessonStudentErrorVM
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = "";
        public string LastWrongAnswer { get; set; } = "";
        public DateTime LastAttemptedAt { get; set; }
        public int WrongAttemptsCount { get; set; }
        public int CorrectAttemptsCount { get; set; }
    }
}
