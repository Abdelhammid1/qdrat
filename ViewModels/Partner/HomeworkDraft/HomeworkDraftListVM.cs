namespace QdratNew.ViewModels.Partner.HomeworkDraft
{
    public class HomeworkDraftListVM
    {
        public int DraftId { get; set; }

        public string Title { get; set; }

        public string CourseName { get; set; }

        public int TotalQuestions { get; set; }

        public DateTime CreatedAt { get; set; }


        public string CurriculumName { get; set; } = string.Empty;
        public int QuestionsCount { get; set; }
        public object CurriculumTitle { get; internal set; }
    }

}
