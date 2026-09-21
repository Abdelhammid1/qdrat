// المسار: ViewModels/Homework/HomeworkQuestionSolveViewModel.cs
using QdratNew.ViewModels.Question;

namespace QdratNew.ViewModels.Homework
{
    public class HomeworkQuestionSolveViewModel
    {
        public int HomeworkId { get; set; }
        public string? SelectedAnswer { get; set; }
        public DateTime StartTime { get; set; } // وقت عرض السؤال
        public int? TimeSpentSeconds { get; set; } // الوقت المستغرق
        public int HomeworkSetId { get; set; }

        public int Order { get; set; } // رقم السؤال في العرض

        public string? AnsweredAt { get; set; }

        public QuestionDisplayViewModel Question { get; set; }

    }

    public class HomeworkSolveSessionViewModel
    {
        public int HomeworkSetId { get; set; }
        public List<HomeworkQuestionSolveViewModel> Questions { get; set; } = new();
    }
}
