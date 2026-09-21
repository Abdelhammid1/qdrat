using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;
using Enums = QdratNew.Enums;
using Entities = QdratNew.Entities;

namespace QdratNew.Entities
{
    public class Question
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public string? Title { get; set; }


        public QuestionTemplate Template { get; set; } = QuestionTemplate.TextOnly;

        public string? ValueA { get; set; }
        public string? ValueB { get; set; }
        public string? ImageUrl { get; set; }

        public string? CorrectAnswer { get; set; }
        public string? Explanation { get; set; }
        public string? VideoUrl { get; set; }

        public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Medium;
        public bool IsQuantitative { get; set; } = false;

        [Required]
        public int CurriculumId { get; set; }
        public Curriculum Curriculum { get; set; }

        public int? SectionId { get; set; }
        public Section Section { get; set; }

        [Required]
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }

        public int AttemptsCount { get; set; }
        public int CorrectAnswersCount { get; set; }

        public bool IsCorrectAnswerAssigned => !string.IsNullOrWhiteSpace(CorrectAnswer);
        public bool IsAnswerConfirmed { get; set; } = false;

        public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();

        // ✅ التصنيفات بنظام Flags
        public QuestionUsageType UsageTypes { get; set; } =
            QuestionUsageType.Assignment |
            QuestionUsageType.Enhancement |
            QuestionUsageType.QdratExam |
            QuestionUsageType.OfficialMockExam |
            QuestionUsageType.PlacementTest |
            QuestionUsageType.PromoTest;

        // ✅ رقم تسلسلي تلقائي
        public int AutoNumber { get; set; }

        // ✅ كود مقروء للسؤال مثل Q-00001
        public string ReferenceNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsReviewed { get; set; } = false;
        public bool IsComplete { get; set; } = false;
        public string? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public bool IsRejected { get; set; } = false;

        public string? LastEditedByUserId { get; set; }
        public DateTime? LastEditedAt { get; set; }

        public int? PartnerId { get; set; }
        public Partner? Partner { get; set; }

        public string? InternalNote { get; set; }
        // ✅ خاصية جديدة: ومضة أو تلميح ذكي
        public string? Hint { get; set; }
        public int? VerbalPassageId { get; set; }
        public VerbalPassage VerbalPassage { get; set; }
        public int? PassageStartSeconds { get; set; }
        public int? PassageEndSeconds { get; set; }
        public string? CreatedByName { get; set; }

        public ViewModels.Question.QuestionDisplayViewModel ToDisplayModel()
        {
            var optionList = this.Options?.ToList() ?? new List<QuestionOption>();
            var correctAnswer = this.CorrectAnswer ?? "";

            return new ViewModels.Question.QuestionDisplayViewModel
            {
                Id = this.Id,
                QuestionId = this.Id,
                Title = this.Title,
                IsAnswerConfirmed = this.IsAnswerConfirmed,
                ImageUrl = this.ImageUrl,
                Hint = this.Hint, // ✅ الجديد
                Explanation = this.Explanation,
                VideoUrl = this.VideoUrl,
                PassageStartSeconds = this.PassageStartSeconds,
                PassageEndSeconds = this.PassageEndSeconds,
                CorrectAnswer = correctAnswer,
                IsQuantitative = this.IsQuantitative,
                Template = this.Template,
                Difficulty = this.Difficulty,
                ComparisonValue1 = this.ValueA,
                ComparisonValue2 = this.ValueB,
                VerbalPassageTitle = this.VerbalPassage != null ? this.VerbalPassage.Title : null,
                VerbalPassageContent = this.VerbalPassage != null ? this.VerbalPassage.Content : null,
                VerbalPassageType = this.VerbalPassage != null
                                    ? this.VerbalPassage.Type
                                    : null,
                VerbalPassageMediaUrl = this.VerbalPassage != null ? this.VerbalPassage.MediaUrl : null,
                VerbalPassageDurationSeconds = this.VerbalPassage != null ? this.VerbalPassage.DurationSeconds : null,
                VerbalPassageRequireFullListen = this.VerbalPassage != null ? this.VerbalPassage.RequireFullListen : false,
                // 🟩 أهم إضافة في النظام بالكامل
                IsRTL = this.Curriculum != null ? this.Curriculum.IsRTL : true,


                // 🟦 هنا الجديد
                LessonTitle = this.Lesson?.Title,
                SectionTitle = this.Lesson?.Section?.Title,
                DisplayType = this.Template switch
                {
                    QuestionTemplate.CompareValues => Enums.QuestionDisplayType.ComparisonText,
                    QuestionTemplate.CompareWithImage => Enums.QuestionDisplayType.ComparisonWithImage,
                    _ => Enums.QuestionDisplayType.WithImage
                },

                // ✅ آمن في حالة عدم وجود إجابة صحيحة أو خيارات فارغة
                SelectedCorrectIndex = optionList.FindIndex(o => o.Text == correctAnswer),

                Options = optionList.Select(o => new ViewModels.Question.QuestionOptionDisplayViewModel
                {
                    Text = o.Text,
                    ImageUrl = o.ImageUrl
                }).ToList()
            };
        }




    }
}
