using QdratNew.Enums;
using QdratNew.ViewModels.Homework;

namespace QdratNew.ViewModels.EnhancementSkills
{
    public class EnhancementReviewQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = "";
        public string? StudentAnswer { get; set; }
        public string? CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public List<QuestionOptionVm> Options { get; set; } = new();
        public string? ImageUrl { get; set; }
        public string? VideoUrl { get; set; }
        public QuestionDisplayType DisplayType { get; set; }
        public string? ComparisonValue1 { get; set; }
        public string? ComparisonValue2 { get; set; }
        public string? VerbalPassageContent { get; set; }
        public bool IsRTL { get; set; } = true;
        public bool IsQuantitative { get; set; }
    }
}
