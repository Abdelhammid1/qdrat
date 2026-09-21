namespace QdratNew.ViewModels.Instructor.Homework
{
    public class HomeworkGlobalReportVM
    {
        public int HomeworkSetId { get; set; }

        public int TotalStudents { get; set; }

        public int SubmittedStudents { get; set; }

        public decimal AverageScore { get; set; }

        public double SuccessRate { get; set; }

        // أصعب سؤال
        public Guid HardestQuestionId { get; set; }

        public string HardestQuestionTitle { get; set; }

        public double HardestQuestionAccuracy { get; set; }

        // أسهل سؤال
        public Guid EasiestQuestionId { get; set; }

        public string EasiestQuestionTitle { get; set; }

        public double EasiestQuestionAccuracy { get; set; }

        // متوسط وقت الإجابة
        public double AverageTimeSeconds { get; set; }

        // الأسئلة
        public List<QuestionGlobalStatVM> Questions { get; set; } = new();

        public int NotSubmittedStudents { get; set; }

        public List<StudentRankingVM> TopStudents { get; set; }

        public List<StudentRankingVM> WeakStudents { get; set; }

        public List<QuestionGlobalStatVM> HardestQuestions { get; set; }

        public List<QuestionGlobalStatVM> EasiestQuestions { get; set; }
    }


    public class QuestionGlobalStatVM
    {
        public Guid QuestionId { get; set; }

        public string QuestionTitle { get; set; }

        public int CorrectAnswers { get; set; }

        public int WrongAnswers { get; set; }

        public int SkippedAnswers { get; set; }

        public double Accuracy { get; set; }

        // توزيع الخيارات
        public List<QuestionOptionStatVM> Options { get; set; } = new();
        public double AverageTimeSeconds { get; set; }
        public Guid HardestQuestionId { get; set; }
        public string HardestQuestionTitle { get; set; }
        public double HardestQuestionAccuracy { get; set; }
        public double EasiestQuestionAccuracy { get; set; }
        public string EasiestQuestionTitle { get; set; }
        public Guid EasiestQuestionId { get; set; }
    }


    public class QuestionOptionStatVM
    {
        public string OptionText { get; set; }

        public int Count { get; set; }

        public double Percentage { get; set; }

        public bool IsCorrect { get; set; }
    }
}