namespace QdratNew.ViewModels.Parents
{
    public class ParentInstitutePlanFollowUpViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public bool HasApprovedPlan { get; set; }
        public double CompletionPercentage { get; set; }
        public bool? AttendedInLab { get; set; }
        public string? NextAppointmentDate { get; set; }
        public string? SafeRecommendation { get; set; }
        public bool IsCompleted { get; set; }
        public string PlanStatusLabel { get; set; } = "لا توجد خطة";
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public int? TotalSessions { get; set; }
        public int? CompletedSessions { get; set; }
    }
}
