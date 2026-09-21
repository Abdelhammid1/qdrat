using System;

namespace QdratNew.ViewModels.PartnerSubscriptions
{
    public class PartnerSubscriptionListItemViewModel
    {
        public int Id { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; }
        public DateTime? AccessUntilDate { get; set; }

        public int? MaxActiveStudents { get; set; }
        public string Status { get; internal set; }

        public int CoursesCount { get; set; }


    }
}
