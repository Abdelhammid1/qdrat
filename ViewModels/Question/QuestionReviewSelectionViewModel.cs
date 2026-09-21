namespace QdratNew.ViewModels.Question
{
    public class QuestionReviewSelectionViewModel
    {
        public int CurriculumId { get; set; }
        public int SectionId { get; set; }
        public int LessonId { get; set; }

        public List<QuestionReviewItemViewModel> AvailableQuestions { get; set; } = new();
    }

    
}
