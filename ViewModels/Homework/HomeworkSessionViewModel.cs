using QdratNew.ViewModels.Question;

namespace QdratNew.ViewModels.Homework
{
    public class HomeworkSessionViewModel
    {
        public int HomeworkSetId { get; set; }
        public string LessonTitle { get; set; }
        public string TrainerName { get; set; }
        public string CurriculumTitle { get; set; }
        public DateTime AssignedAt { get; set; }

        public List<QuestionDisplayViewModel> Questions { get; set; } = new();
    }

}
