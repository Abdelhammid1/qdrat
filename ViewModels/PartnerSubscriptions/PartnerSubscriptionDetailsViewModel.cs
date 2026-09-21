using System;

namespace QdratNew.ViewModels.PartnerSubscriptions
{
    public class PartnerSubscriptionDetailsViewModel
    {
        public string PartnerName { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public bool CanUseProfessionalModels { get; set; }

        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; }

        public int? MaxActiveStudents { get; set; }

        public bool AllowMultipleBranches { get; set; }

        public bool CanUseQuestionBank { get; set; }

        public bool CanCreateHomework { get; set; }

        public bool CanCreateExams { get; set; }

        public bool CanUsePlacementExams { get; set; }

        public bool CanUsePerformanceIndicatorExams { get; set; }

        public bool CanUseReinforcementSkills { get; set; }

        public bool CanUseRemedialPlans { get; set; }

        public bool CanUseRemedialSessions { get; set; }

        public bool CanAccessEducationalContent { get; set; }

        public DateTime? AccessUntilDate { get; set; }
        public int? MaxInstructors { get; set; }
        public int CurrentInstructorsCount { get; set; }
    }
}
