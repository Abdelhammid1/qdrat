using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.ViewModels.Students
{
    public class StudentSessionReservationListViewModel
    {
        public int Id { get; set; }
        public string BranchName { get; set; }
        public string InstructorName { get; set; }
        public DateTime RequestedDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int TotalSeats { get; set; }
        public decimal? Fee { get; set; }
        public bool IsCompleted { get; set; }  // تم تنفيذ الجلسة فعليًا
        public bool IsRated { get; set; }      // هل الطالب قيّم الجلسة مسبقًا

        public StudySessionStatus Status { get; set; }

    }
}
