namespace QdratNew.ViewModels.Partner.Exam
{
    public class ExamDraftLessonGroupVM
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = "";

        public List<ExamDraftQuestionItemVM> Questions { get; set; }
            = new();
    }
}
