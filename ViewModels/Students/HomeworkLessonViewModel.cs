namespace QdratNew.ViewModels.Students
{
   
    public class HomeworkLessonViewModel
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }
        public string UnitTitle { get; set; }
        public int QuestionCount { get; set; }
        public bool IsCompleted { get; set; }
        public List<int> HomeworkIds { get; set; }
    }

}
