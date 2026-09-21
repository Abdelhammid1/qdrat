using System.Collections.Generic;

namespace QdratNew.ViewModels.Homework
{
    public class LessonQuestionsReviewVm
    {
        public int HomeworkSetId { get; set; }
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = "";
        public List<HomeworkQuestionResultItem> Questions { get; set; } = new();
    }
}
