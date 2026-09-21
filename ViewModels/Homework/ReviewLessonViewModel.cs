using QdratNew.Enums;

namespace QdratNew.ViewModels.Homework
{
    public class ReviewLessonViewModel
    {
        public int HomeworkSetId { get; set; }
        public string HomeworkSetTitle { get; set; } = "";
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = "";
        public string SectionTitle { get; set; } = "";
        public int TotalStudents { get; set; }
        public List<ReviewLessonQuestionVm> Questions { get; set; } = new();
    }

    public class ReviewLessonQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string QuestionTitle { get; set; } = "";
        public string? ImageUrl { get; set; }
        public string? VerbalPassageTitle { get; set; }
        public string? VerbalPassageContent { get; set; }
        public string? ComparisonValue1 { get; set; }
        public string? ComparisonValue2 { get; set; }
        public QuestionDisplayType DisplayType { get; set; }
        public bool IsQuantitative { get; set; }
        public string? CorrectAnswer { get; set; }

        public int TotalAnswered { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public double SuccessRate => TotalAnswered == 0 ? 0 : Math.Round(CorrectCount * 100.0 / TotalAnswered, 1);

        public List<ReviewLessonOptionVm> Options { get; set; } = new();
    }

    public class ReviewLessonOptionVm
    {
        public string Text { get; set; } = "";
        public string? ImageUrl { get; set; }
        public bool IsCorrect { get; set; }
        public int SelectedCount { get; set; }
        public double SelectionRate { get; set; }
    }
}
