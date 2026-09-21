namespace QdratNew.ViewModels.StudySessions
{
    public class StudySessionReservationViewModel
    {
        public int Id { get; set; }
        public string StudentName { get; set; }
        public string BranchName { get; set; }
        public string? InstructorName { get; set; }
        public DateTime RequestedDate { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Status { get; set; }
        public decimal? Fee { get; set; }
        public string AdditionalServices { get; set; }
    }
}
