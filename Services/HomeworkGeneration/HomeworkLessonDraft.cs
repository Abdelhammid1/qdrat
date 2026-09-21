namespace QdratNew.Services.HomeworkGeneration
{
    public class HomeworkLessonDraft
    {
        public int LessonId { get; set; }
        public string LessonName { get; set; }

        public List<QuestionDraft> Questions { get; set; } = new();
    }
}
