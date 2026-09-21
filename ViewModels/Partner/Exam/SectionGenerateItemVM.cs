namespace QdratNew.ViewModels.Partner.Exam
{
    public class SectionGenerateItemVM
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }

        public int QuestionCount { get; set; }
     

        public List<LessonGenerateItemVM> Lessons { get; set; }
            = new List<LessonGenerateItemVM>();
    }
    public class LessonGenerateItemVM
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }

        public int QuestionCount { get; set; }
    }


}
