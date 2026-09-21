using QdratNew.Entities;

public static class QuestionCompletenessChecker
{
    public static bool IsComplete(Question question)
    {
        var hasTitle = !string.IsNullOrWhiteSpace(question.Title);

        // يجب أن تكون هناك 4 خيارات على الأقل وكلها غير فارغة
        var hasValidOptions = question.Options != null
            && question.Options.Count >= 4
            && question.Options.All(opt => !string.IsNullOrWhiteSpace(opt.Text));

        // يجب أن تكون هناك إجابة صحيحة
        var hasAnswer = !string.IsNullOrWhiteSpace(question.CorrectAnswer);

        return hasTitle && hasValidOptions && hasAnswer;
    }
}
