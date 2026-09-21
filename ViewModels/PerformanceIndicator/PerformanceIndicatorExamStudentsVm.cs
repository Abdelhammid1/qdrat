namespace QdratNew.ViewModels.PerformanceIndicator
{
    public class PerformanceIndicatorExamStudentsVm
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; }
        public string CurriculumTitle { get; set; }

        public List<PerformanceIndicatorExamStudentResultVm> Students { get; set; }
            = new();
    }

    public class PerformanceIndicatorExamStudentResultVm
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public double ScorePercent { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }

        public bool HasRemedialRecommendation { get; set; }

        public List<SectionPerformanceVm> WeakSections { get; set; }
            = new();
    }

    public class SectionPerformanceVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }

        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public double ScorePercent { get; set; }

        public bool NeedsRemedial { get; set; }
        public double Accuracy { get; internal set; }
    }

    public class PerformanceIndicatorExamStudentsReportViewModel
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public string CurriculumTitle { get; set; } = string.Empty;
        public int TotalStudents { get; set; }
        public int CompletedStudents { get; set; }
        public int PassedStudents { get; set; }
        public int NeedRemedialPlanStudents { get; set; }
        public double AverageScore { get; set; }
        public double PassPercent { get; set; } = 60;
        public List<PerformanceIndicatorExamStudentRowViewModel> Students { get; set; } = new();
    }

    public class PerformanceIndicatorExamStudentRowViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double? ScorePercent { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public bool NeedsRemedialPlan { get; set; }
        public List<StudentWeakSectionViewModel> WeakSections { get; set; } = new();

        /// <summary>true = محاولة اختبار مؤشر الأداء هذه موقوفة حاليًا بسبب رصد ترجمة المتصفح (Translation Guard)</summary>
        public bool IsIntegrityBlocked { get; set; }
    }

    public class StudentWeakSectionViewModel
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public double ScorePercent { get; set; }
        public int CorrectCount { get; set; }
        public int TotalQuestions { get; set; }
    }

    public class StudentPerformanceIndicatorQuestionReportViewModel
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int SkippedCount { get; set; }
        public double ScorePercent { get; set; }
        public List<StudentPerformanceIndicatorQuestionRowViewModel> Questions { get; set; } = new();
    }

    public class StudentPerformanceIndicatorQuestionRowViewModel
    {
        public Guid QuestionId { get; set; }
        public int OrderNumber { get; set; }
        public string QuestionTitle { get; set; } = string.Empty;
        public int? SectionId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public int? LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public string StudentAnswer { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public bool IsSkipped { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public double? TimeTakenSeconds { get; set; }
    }

}
