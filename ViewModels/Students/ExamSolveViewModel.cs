using QdratNew.Enums;
using QdratNew.ViewModels.Question;

namespace QdratNew.ViewModels.Students
{
    public class ExamSolveViewModel
    {
        public int ExamAssignmentId { get; set; }
        public string ExamTitle { get; set; }
        public List<QuestionSolveViewModel> Questions { get; set; } = new();
        public bool IsReviewMode { get; set; } = false;

        public int ExamId { get; set; }
        public Guid CurrentQuestionId { get; set; }
        public List<Guid> AllQuestionIds { get; set; } = new();

        public QuestionDisplayViewModel Question { get; set; }
        public string? SelectedAnswer { get; set; }

        public int DurationMinutes { get; set; }
        public DateTime StartTime { get; set; }

        public bool IsSubmitted { get; set; }

        public int CurrentIndex => AllQuestionIds.IndexOf(CurrentQuestionId) + 1;
        public int Total => AllQuestionIds.Count;

        public bool ForceReviewVisible { get; set; }



        public Dictionary<Guid, string> AnswersMap { get; set; } = new();
        public DateTime QuestionStartTime { get; set; }


        public bool IsExamSubmitted { get; set; } = false;
        public int RemainingSeconds { get; set; } // الوقت المتبقي من السيرفر

        public List<Guid> ReviewMarkedIds { get; set; } = new(); // لو حددها للمراجعة
        public bool ShowFooterNavigation { get; set; } = true;


        public Guid QuestionId { get; set; }

        public string QuestionTitle { get; set; }

        public string ImageUrl { get; set; }

        public string LessonTitle { get; set; }

        public string SectionTitle { get; set; }

        public List<QuestionOptionDisplayViewModel> Options { get; set; } = new();

        public string CorrectAnswer { get; set; }

        public bool IsQuantitative { get; set; }

        public string SelectedOption { get; set; }

        public double TimeTakenSeconds { get; set; }



        public bool IsLastQuestion { get; set; } = false;

        public bool IsFirstQuestion { get; set; } = false;

        // 🟩 القطعة اللفظية (مفترض تكون مضافة هنا)
        public string? VerbalPassageTitle { get; set; }
        public string? VerbalPassageContent { get; set; }


        public bool IsIndividual { get; set; }

        public Dictionary<Guid, bool> AnsweredQuestions { get; set; } = new();
        public bool IsRTL { get; internal set; }
        public string? VerbalPassageMediaUrl { get; internal set; }
        public PassageType? VerbalPassageType { get; internal set; }
        public bool VerbalPassageRequireFullListen { get; internal set; }
        public int? VerbalPassageDurationSeconds { get; internal set; }

        public string SubmitAnswerUrl { get; set; } = string.Empty;
        public string FinalSubmitUrl { get; set; } = string.Empty;
        public string ExamType { get; set; } = "General";
    }

    public class QuestionSolveViewModel
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public List<string> Options { get; set; } = new();
    }

}
