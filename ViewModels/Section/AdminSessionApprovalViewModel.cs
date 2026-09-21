using QdratNew.Enums;

namespace QdratNew.ViewModels.Section
{
    public class AdminSessionApprovalViewModel
    {
        public int Id { get; set; }
        public string StudentName { get; set; }
        public string BranchName { get; set; }
        public string InstructorName { get; set; }
        public DateTime RequestedDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int TotalSeats { get; set; }
        public decimal? BaseFee { get; set; } // ✅ إدخال من الأدمن
        public string AdditionalServices { get; set; } // ✅ إدخال من الأدمن
        public bool IsCompleted { get; set; }

        public StudySessionStatus Status { get; set; }

    }

}
