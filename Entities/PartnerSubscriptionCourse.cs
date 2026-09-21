namespace QdratNew.Entities
{
    public class PartnerSubscriptionCourse
    {
        public int Id { get; set; }

        public int PartnerSubscriptionId { get; set; }
        public PartnerSubscription PartnerSubscription { get; set; }

        public int CourseId { get; set; }
        public Course Course { get; set; }

        // 🔐 صلاحيات بنك الأسئلة
        public bool CanUsePlatformQuestionBank { get; set; } = true;
        public bool CanCreatePrivateQuestions { get; set; } = false;



    }
}
