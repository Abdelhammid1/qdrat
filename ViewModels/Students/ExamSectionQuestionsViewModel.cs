using QdratNew.ViewModels.Exam;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Students
{
    public class ExamSectionQuestionsViewModel
    {
        public string SectionTitle { get; set; }
        public List<ExamLessonQuestionsVm> Lessons { get; set; } = new();
        public int ExamAssignmentId { get; set; }
        public int StudentId { get; set; }
        public int SectionId { get; set; }
    }
    public class ExamLessonQuestionsVm
    {
        public string LessonTitle { get; set; }
        public List<ExamQuestionEntry> Questions { get; set; } = new();
    }
    public class ExamLessonQuestionsViewModel
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }
        public int AssignmentId { get; set; }
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
     

        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public string AvgTimeFormatted { get; set; }

        public List<QuestionAnalyticsVm> Questions { get; set; } = new();
    }

    public class ExamQuestionEntry
    {
        public string QuestionTitle { get; set; }
        public string StudentAnswer { get; set; }
        public string CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsSkipped { get; set; }
        public string StatusText { get; set; }

    
    }
}
