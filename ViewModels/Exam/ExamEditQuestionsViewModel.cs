namespace QdratNew.ViewModels.Exam
{
    public class ExamEditQuestionsViewModel
    {
        public int AssignmentId { get; set; }
        public string AssignmentTitle { get; set; }

        public List<ExamQuestionRow> Questions { get; set; } = new();
    }

    public class ExamQuestionRow
    {
        public int? Id { get; set; }        // ExamQuestion Id
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public int Order { get; set; }
    }

}
