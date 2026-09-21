namespace QdratNew.ViewModels.Partner.Dashboard
{
    public class PartnerInstructorSummaryViewModel
    {
        public int InstructorId { get; set; }
        public string InstructorName { get; set; } = string.Empty;

        public int LecturesCount { get; set; }
        public decimal AverageAttendanceRate { get; set; }

        public decimal StudentPerformanceAverage { get; set; }

        public DateTime? LastActivityDate { get; set; }
    }
}
