using QdratNew.Enums;

namespace QdratNew.ViewModels.Question
{
    public class QuestionLabelModalViewModel
    {
        public Guid QuestionId { get; set; }
        public List<string> SelectedLabels { get; set; } = new();
        public QuestionUsageType UsageTypes { get; set; }

    }
}
