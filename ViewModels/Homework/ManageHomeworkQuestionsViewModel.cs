namespace QdratNew.ViewModels.Homework
{
    public class ManageHomeworkQuestionsViewModel
    {
        public int HomeworkSetId { get; set; }
        public string HomeworkTitle { get; set; }
        public List<HomeworkQuestionManageItem> ExistingQuestions { get; set; } = new();
        public List<HomeworkQuestionManageItem> SearchResults { get; set; } = new();
        public string SearchTerm { get; set; }
    }

    public class HomeworkQuestionManageItem
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public string InternalNote { get; set; }
        public string LessonTitle { get; set; }
        public bool IsReviewed { get; set; }
        public bool HasImage { get; set; }
    }

}
