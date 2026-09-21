using System.Collections.Generic;

namespace QdratNew.ViewModels.HomeworkGeneration
{
    public class HomeworkDraftLessonViewModel
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;

        public int QuestionsCount => Questions.Count;

        public List<HomeworkDraftQuestionViewModel> Questions { get; set; } = new();
    }
}
