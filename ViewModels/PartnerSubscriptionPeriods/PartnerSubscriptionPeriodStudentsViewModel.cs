namespace QdratNew.ViewModels.PartnerSubscriptionPeriods
{
    public class PartnerSubscriptionPeriodStudentsViewModel
    {
        public int PeriodId { get; set; }
        public string PartnerName { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int StudentId { get; set; }

        public string? UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string? NationalId { get; set; }

        public string? PhoneNumber { get; set; }

        public bool IsActiveForLearning { get; set; }



        public List<PartnerSubscriptionPeriodStudentViewModel> Students { get; set; }
            = new();
    }

}
