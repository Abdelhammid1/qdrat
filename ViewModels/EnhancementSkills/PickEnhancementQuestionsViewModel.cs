using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace QdratNew.ViewModels.EnhancementSkills
{
    public class PickEnhancementQuestionsViewModel
    {
        public int SetId { get; set; }
        public int BatchId { get; set; }
        public string SetTitle { get; set; } = "";
        public string LectureTitle { get; set; } = "";
        public string BatchName { get; set; } = "";

        [ValidateNever]
        public List<QuestionItem> AvailableQuestions { get; set; } = new();

        public List<Guid> SelectedQuestionIds { get; set; } = new();

        public class QuestionItem
        {
            public Guid QuestionId { get; set; }
            public string ReferenceNumber { get; set; } = "";
            public string Title { get; set; } = "";
            public string LessonTitle { get; set; } = "";
            public string SectionTitle { get; set; } = "";
            public bool IsSelected { get; set; }
        }
    }
}
