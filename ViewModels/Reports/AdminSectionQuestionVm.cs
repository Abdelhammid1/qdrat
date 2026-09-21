using QdratNew.ViewModels.Homework;

namespace QdratNew.ViewModels.Reports
{
    public class AdminSectionQuestionVm
    {
        public string SectionTitle { get; set; }
        public string LessonTitle { get; set; }

        public string QuestionTitle { get; set; }
        public string VerbalPassageContent { get; set; }
        public string ImageUrl { get; set; }

        public bool IsQuantitative { get; set; }

        public string ComparisonValue1 { get; set; }
        public string ComparisonValue2 { get; set; }

        public QdratNew.Enums.QuestionDisplayType DisplayType { get; set; }

        public List<QuestionOptionVm> Options { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? StudentAnswer { get; set; }
        public bool IsCorrect { get; internal set; }
    }

}
