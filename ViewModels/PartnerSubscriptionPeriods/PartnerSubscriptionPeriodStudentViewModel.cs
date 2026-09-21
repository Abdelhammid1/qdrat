namespace QdratNew.ViewModels.PartnerSubscriptionPeriods
{
    public class PartnerSubscriptionPeriodStudentViewModel
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public string NationalId { get; set; }
        public string PhoneNumber { get; set; }
        public bool IsActiveForLearning { get; set; }
        public string? UserId { get; internal set; }
    }

}
