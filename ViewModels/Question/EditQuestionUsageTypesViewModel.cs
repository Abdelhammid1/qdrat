using QdratNew.Enums;

namespace QdratNew.ViewModels.Question
{
    public class EditQuestionUsageTypesViewModel
    {

    
        public Guid QuestionId { get; set; }
        public QuestionUsageType SelectedUsageTypes { get; set; }
        public List<QuestionUsageType> AllUsageTypes { get; set; } = new();



    }
}
