namespace QdratNew.ViewModels.Partner.HomeworkDraft
{
    public class GenerateHomeworkDraftVM
    {
        public string Title { get; set; } = string.Empty;

        public int CurriculumId { get; set; }

        // الأسئلة المختارة (ناتجة من التوليد أو الاختيار)
        public List<Guid> QuestionIds { get; set; } = new();
    }

}
