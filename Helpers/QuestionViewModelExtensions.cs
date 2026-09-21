using QdratNew.Entities;
using QdratNew.ViewModels.Question;
using QdratNew.Helpers;
using System.Linq;

namespace QdratNew.Helpers
{
    public static class QuestionExtensions
    {
        public static QuestionDisplayViewModel ToDisplayModel(this Question question)
        {
            return new QuestionDisplayViewModel
            {
                Id = question.Id,
                Title = question.Title,
              
                ComparisonValue1 = question.ValueA, // ✅ القيم المطلوبة للعرض
                ComparisonValue2 = question.ValueB,
                Template = question.Template,
                Difficulty = question.Difficulty,
                Explanation = question.Explanation,
                VideoUrl = question.VideoUrl,
                ImageUrl = question.ImageUrl,
                CorrectAnswer = question.CorrectAnswer,
                IsQuantitative = question.IsQuantitative,
                IsAnswerConfirmed = !string.IsNullOrWhiteSpace(question.CorrectAnswer),
                Options = question.Options.Select(o => new QuestionOptionDisplayViewModel
                {
                    Text = o.Text,
                    ImageUrl = o.ImageUrl
                }).ToList(),
                DisplayType = QuestionHelper.GetDisplayType(question)
            };
        }
    }
}
