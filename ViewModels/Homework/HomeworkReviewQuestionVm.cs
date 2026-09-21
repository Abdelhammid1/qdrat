using QdratNew.Entities;

namespace QdratNew.ViewModels.Homework
{
    public class HomeworkReviewQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string CorrectAnswer { get; set; }
        public string StudentAnswer { get; set; }
        public bool IsCorrect { get; set; }


        // خصائص إضافية لعرض كل أشكال السؤال
        public string? ImageUrl { get; set; }
        public string? VerbalPassageContent { get; set; }
        public string? ComparisonValue1 { get; set; }
        public string? ComparisonValue2 { get; set; }
        public QdratNew.Enums.QuestionDisplayType DisplayType { get; set; }
        public bool IsQuantitative { get; set; }

        // الاختيارات
        public List<QuestionOptionVm> Options { get; set; } = new();
        // 🕒 الوقت المستغرق
        public double TimeTakenSeconds { get; set; }
        public string? VerbalPassageTitle { get; set; }  // ✅
    }
}
