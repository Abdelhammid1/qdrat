using QdratNew.Enums;

namespace QdratNew.ViewModels.Question
{
    public class QuestionDetailsViewModel
    {
        // ✅ معلومات تعريفية
        public Guid Id { get; set; }

        // ✅ نص السؤال والقيم
        public string Title { get; set; }
        public string? ValueA { get; set; }
        public string? ValueB { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? Explanation { get; set; }
        public string? VideoUrl { get; set; }
        public string? ImageUrl { get; set; }

        // ✅ خصائص العرض والتحكم
        public bool IsQuantitative { get; set; }
        public bool IsAnswerConfirmed => !string.IsNullOrWhiteSpace(CorrectAnswer);
        public DateTime CreatedAt { get; set; }

        // ✅ التصنيفات المرتبطة بالسؤال
        public DifficultyLevel Difficulty { get; set; }
        public QuestionTemplate Template { get; set; }

        // ✅ العناوين المرجعية
        public string CurriculumTitle { get; set; }
        public string SectionTitle { get; set; }
        public string LessonTitle { get; set; }

        // ✅ الاختيارات
        public List<QuestionOptionViewModel> Options { get; set; }

        // ✅ تحويل إلى ViewModel للعرض الموحد
        public QuestionDisplayViewModel ToDisplayModel()
        {
            bool hasImage = !string.IsNullOrWhiteSpace(ImageUrl);
            bool hasComparison = !string.IsNullOrWhiteSpace(ValueA) && !string.IsNullOrWhiteSpace(ValueB);

            var displayType = QuestionDisplayType.TextOnly;
            if (hasImage && hasComparison)
                displayType = QuestionDisplayType.ComparisonWithImage;
            else if (hasComparison)
                displayType = QuestionDisplayType.ComparisonText;
            else if (hasImage)
                displayType = QuestionDisplayType.WithImage;

            var correctIndex = Options.FindIndex(o => o.Text?.Trim() == CorrectAnswer?.Trim());

            return new QuestionDisplayViewModel
            {
                Id = Id,
                Title = Title,
                ComparisonValue1 = ValueA,
                ComparisonValue2 = ValueB,
                ImageUrl = ImageUrl,
                Explanation = Explanation,
                VideoUrl = VideoUrl,
                Template = (QuestionTemplate)Template,
                Difficulty = (DifficultyLevel)Difficulty,
                IsQuantitative = IsQuantitative,
                CorrectAnswer = CorrectAnswer,
                IsAnswerConfirmed = IsAnswerConfirmed,
                SelectedCorrectIndex = correctIndex >= 0 ? correctIndex : null,
                Options = Options.Select(o => new QuestionOptionDisplayViewModel
                {
                    Text = o.Text,
                    ImageUrl = o.ImageUrl
                }).ToList()
            };
        }

    }
}
