namespace QdratNew.ViewModels.EnhancementSkills
{
    public class EnhancementSkillQuestionViewModel
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public string? ValueA { get; set; }
        public string? ValueB { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> Options { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? Explanation { get; set; }
        public bool IsQuantitative { get; set; }
    }
}
