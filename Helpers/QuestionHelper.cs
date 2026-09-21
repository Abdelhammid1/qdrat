using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Helpers
{
    public static class QuestionHelper
    {
        public static QuestionDisplayType GetDisplayType(Question question)
        {
            if (!string.IsNullOrEmpty(question.ImageUrl) && !string.IsNullOrEmpty(question.ValueA) && !string.IsNullOrEmpty(question.ValueB))
            {
                return QuestionDisplayType.ComparisonWithImage;
            }

            if (!string.IsNullOrEmpty(question.ImageUrl))
            {
                return QuestionDisplayType.WithImage;
            }

            if (!string.IsNullOrEmpty(question.ValueA) && !string.IsNullOrEmpty(question.ValueB))
            {
                return QuestionDisplayType.ComparisonText;
            }

            return QuestionDisplayType.TextOnly;
        }
    }
}
