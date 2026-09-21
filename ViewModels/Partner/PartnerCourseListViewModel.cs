namespace QdratNew.ViewModels.Partner
{
    public class PartnerCourseListViewModel
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; }

        public bool CanUsePlatformQuestionBank { get; set; }
        public bool CanCreatePrivateQuestions { get; set; }

        public int BatchesCount { get; set; }



    }
}
