using QdratNew.Enums;

namespace QdratNew.ViewModels.Exam
{
    public class ReplaceQuestionViewModel
    {
        public int ExamId { get; set; }
        public int SectionId { get; set; }
        public Guid OldQuestionId { get; set; }
        public Guid NewQuestionId { get; set; }

        public string? OldQuestionTitle { get; set; }

        public List<QdratNew.ViewModels.Question.QuestionSimpleViewModel> AvailableQuestions { get; set; } = new();
        public Guid QuestionId { get; internal set; }
        public string? Title { get; internal set; }
        public DifficultyLevel Difficulty { get; internal set; }
        public string? InternalNote { get; set; }   // ✅ الجديد


    }







}
