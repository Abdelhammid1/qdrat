namespace QdratNew.ViewModels.Exam
{
    public class AddQuestionFromBankVm
    {
        public int ExamId { get; set; }
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public List<AddQuestionFromBankItemVm> Questions { get; set; } = new();
    }

    public class AddQuestionFromBankItemVm
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public string ImageUrl { get; set; }
        public bool IsAlreadyAdded { get; set; }
    }

}
