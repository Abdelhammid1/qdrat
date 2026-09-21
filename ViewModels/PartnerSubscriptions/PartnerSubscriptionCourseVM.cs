namespace QdratNew.ViewModels.PartnerSubscriptions
{
    public class PartnerSubscriptionCourseVM
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; }

        public bool IsSelected { get; set; }

        public bool CanUsePlatformQuestionBank { get; set; }
        public bool CanCreatePrivateQuestions { get; set; }
        


    }

}
