// ViewModels/Question/ApproveReadyQuestionsViewModel.cs
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Question
{
    public class ApproveReadyQuestionsViewModel
    {
        public int? CurriculumId { get; set; }
        public int? SectionId { get; set; }
        public int? LessonId { get; set; }

        public List<SelectListItem> Curriculums { get; set; } = new();
        public List<SelectListItem> Sections { get; set; } = new();
        public List<SelectListItem> Lessons { get; set; } = new();
        public List<QuestionListViewModel> Questions { get; set; } = new();

        public List<QuestionListViewModel> ReadyQuestions { get; set; } = new();

        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }

    }
}
