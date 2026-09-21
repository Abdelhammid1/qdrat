using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;
using QdratNew.Entities;

namespace QdratNew.ViewModels.Question
{
    public class QuestionListViewModel
    {
        // ✅ التعريفات العامة
        public Guid Id { get; set; }
        public string ReferenceNumber { get; set; } = "—";
        public string Title { get; set; }
        public string TitlePreview { get; set; }

        // ✅ التصنيفات التربوية
        public string CurriculumTitle { get; set; } = "—";
        public string SectionTitle { get; set; } = "—";
        public string LessonTitle { get; set; } = "—";

        // ✅ خصائص المحتوى
        public string? ValueA { get; set; }
        public string? ValueB { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsQuantitative { get; set; }
        public string? Explanation { get; set; }
        public string? CorrectAnswer { get; set; }

        [Display(Name = "التلميح")]
        public string? InternalNote { get; set; }

        // ✅ حالة المراجعة
        public bool IsReviewed { get; set; }
        public bool IsComplete { get; set; }
        public bool IsRejected { get; set; }
        public bool IsAnswerConfirmed { get; set; }
        public bool LessonIsActive { get; set; } // 🔴 الجديد

        // ✅ التصنيفات والخيارات
        public List<string> SelectedLabels { get; set; } = new();
        public List<QuestionOption> Options { get; set; } = new();

        // ✅ معلومات إضافية
        public DateTime CreatedAt { get; set; }
        public QuestionAuditLog? LatestAudit { get; set; }


        public string? VideoUrl { get; set; } // ✅ أضف هذا السطر

        public string? ComparisonValue1 { get; set; }
        public string? ComparisonValue2 { get; set; }


        // ✅ النوع والصعوبة (باستخدام Enums الخاصة بـ QdratNew.Enums)
        public QdratNew.Enums.QuestionTemplate Template { get; set; }
        public QdratNew.Enums.DifficultyLevel Difficulty { get; set; }

        // ✅ التصنيفات الكاملة لاستخدامها في المودال
        public QuestionUsageType UsageTypes { get; set; }

        // ✅ عرض تلخيص التصنيفات
        public string ClassificationSummary => string.Join("، ", SelectedLabels);

        // ✅ تحويل لعرض موحّد داخل البارشال
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
                ComparisonValue1 = ValueA, // ✅ مهم
                ComparisonValue2 = ValueB, // ✅ مهم
                ImageUrl = ImageUrl,
                Explanation = Explanation,
                VideoUrl = VideoUrl,
                Template = (QuestionTemplate)Template,
                Difficulty = (DifficultyLevel)Difficulty,
                IsQuantitative = IsQuantitative,
                CorrectAnswer = CorrectAnswer,
                IsAnswerConfirmed = IsAnswerConfirmed,
                SelectedCorrectIndex = correctIndex >= 0 ? correctIndex : null,
                DisplayType = displayType, // ✅ لا تنسى تمرير هذا أيضاً
                Options = Options.Select(o => new QuestionOptionDisplayViewModel
                {
                    Text = o.Text,
                    ImageUrl = o.ImageUrl
                }).ToList()
            };
        }


    }
}
