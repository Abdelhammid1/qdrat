using QdratNew.ViewModels.Question;

namespace QdratNew.ViewModels.EnhancementSkills
{
    public class EnhancementSkillSolveViewModel
    {
        public int BatchId { get; set; }
        public List<QuestionDisplayViewModel> Questions { get; set; } = new();
        public int CurrentIndex { get; set; }
        public List<string?> SelectedAnswers { get; set; } = new();
    }

}
