using QdratNew.ViewModels.Question;
using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Homework
{
    public class StudentHomeworkResultViewModel
    {
        public int HomeworkSetId { get; set; }
        public string CurriculumTitle { get; set; }
        public string SectionTitle { get; set; }
        public string LessonTitle { get; set; }
        public DateTime SubmittedAt { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public string MotivationalMessage { get; set; }

        public List<QuestionDisplayViewModel> WrongQuestions { get; set; } = new();

        // ✅ تم التعديل إلى Guid بدلاً من int
        public Dictionary<Guid, string> VideoLinksByQuestionId { get; set; } = new();


        public int WrongAnswers => TotalQuestions - CorrectAnswers;

        public double ScorePercentage => TotalQuestions > 0
            ? Math.Round((double)CorrectAnswers / TotalQuestions * 100, 2)
            : 0;

    }
}
