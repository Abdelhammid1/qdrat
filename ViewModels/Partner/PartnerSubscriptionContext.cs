namespace QdratNew.ViewModels.Partner
{
    public class PartnerSubscriptionContext
    {
        public int PartnerId { get; set; }
        public int SubscriptionId { get; set; }
        public int ActivePeriodId { get; set; }

        // =========================
        // 🔐 صلاحيات العقد
        // =========================
        public bool CanCreateHomework { get; set; }
        public bool CanCreateExams { get; set; }

        public bool CanUsePlacementExams { get; set; }
        public bool CanUsePerformanceIndicatorExams { get; set; }

        public bool CanUseReinforcementSkills { get; set; }
        public bool CanUseRemedialPlans { get; set; }
        public bool CanUseRemedialSessions { get; set; }

        public bool CanAccessEducationalContent { get; set; }
        public bool CanUseQuestionBank { get; set; }
        public bool CanCreateMultipleBranches { get; set; }

        public bool CanUseProfessionalModels { get; set; }
        public bool CanUseAIAnalytics { get; set; }

        // =========================
        // 📚 الدورات المتاحة
        // =========================

        public List<PartnerCourseContext> Courses { get; set; } = new();
    }

    public class PartnerCourseContext
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public bool CanUsePlatformQuestionBank { get; set; }
    }
}
