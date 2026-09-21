
namespace QdratNew.ViewModels.Lesson
{
    public class LessonQuestionReviewViewModel
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }
        public string CurriculumTitle { get; set; }

        public List<QdratNew.Entities.Question> Questions { get; set; } = new();
    }
}
