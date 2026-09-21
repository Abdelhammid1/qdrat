namespace QdratNew.ViewModels.Homework
{
    public class ConfirmedLessonItem
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = "";
        public int SectionId { get; set; } = 0;
        public int TotalQuestionsAvailable { get; set; }
        public int SelectedQuestionCount { get; set; }
        public int QuestionsToUse { get; set; }
    }
}
