using System;

namespace QdratNew.ViewModels.PartnerSubscriptionPeriods
{
    public class PartnerSubscriptionPeriodDetailsViewModel
    {
        public string PartnerName { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int? MaxStudents { get; set; }

        public bool IsActive { get; set; }

        public int StudentsCount { get; set; }




        public int PeriodId { get; set; }

  

        public int CurrentStudentsCount { get; set; }

     

        // عرض الطلاب (اختياري الآن – للعرض فقط)
        public List<StudentInPeriodItem> Students { get; set; } = new();
    }

    public class StudentInPeriodItem
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public string NationalId { get; set; }
        public DateTime RegistrationDate { get; set; }
        public bool IsActiveForLearning { get; set; }
    }

}

