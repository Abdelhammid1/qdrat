namespace QdratNew.ViewModels.Partner
{
    public class PartnerSidebarPermissionsVM
    {
        // ===== اللوجو =====
        public string? PartnerLogoPath { get; set; }

        public bool CanCreateHomework { get; set; }
        public bool CanCreateExams { get; set; }

        public bool CanUsePlacementExams { get; set; }
        public bool CanUsePerformanceIndicatorExams { get; set; }

        public bool CanUseReinforcementSkills { get; set; }
        public bool CanUseRemedialPlans { get; set; }
        public bool CanUseRemedialSessions { get; set; }
        public bool CanManageInstructors { get; set; }
        public bool CanAccessEducationalContent { get; set; }
        public bool CanUseProfessionalModels { get; set; }
    }

}
