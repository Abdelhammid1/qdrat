using QdratNew.Entities;
using QdratNew.ViewModels.Partner;

namespace QdratNew.Interfaces
{
    public interface IPartnerSubscriptionService
    {
        // =========================================
        // 🔹 الاشتراك
        // =========================================
        PartnerSubscription? GetActiveSubscription(int partnerId);

        // =========================================
        // 🔹 الطلاب
        // =========================================
        bool CanAddStudents(int partnerId, int studentsToAdd = 1);

        // =========================================
        // 🔹 الواجبات والاختبارات
        // =========================================
        bool CanUseHomework(int partnerId);

        bool CanUseExams(int partnerId);

        // =========================================
        // 🔹 اختبارات متقدمة
        // =========================================
        bool CanUsePlacementExams(int partnerId);

        bool CanUsePerformanceIndicatorExams(int partnerId);

        // =========================================
        // 🔹 الخطط العلاجية والمهارات
        // =========================================
        bool CanUseRemedialPlans(int partnerId);

        bool CanUseRemedialSessions(int partnerId);

        bool CanUseReinforcementSkills(int partnerId);
        bool CanUseAIAnalytics(int partnerId);



        // =========================================
        // 🔹 فترات الاشتراك (Periods)
        // =========================================
        PartnerSubscriptionPeriod? GetActivePeriod(int partnerId);
        PartnerSubscriptionContext? GetActiveContext(int partnerId);

        IQueryable<PartnerSubscriptionPeriod> GetAllPeriods(int partnerId);

        // =========================================
        // 🔹 المحتوى التعليمي
        // =========================================
        bool CanAccessEducationalContent(int partnerId);

        // =========================================
        // 🔹 صلاحيات عامة
        // =========================================
        bool CanUseQuestionBank(int partnerId);

        bool CanCreateMultipleBranches(int partnerId);

        // =========================================
        // 🔹 الوصول للبيانات
        // =========================================
        bool HasDataAccess(int partnerId);
    }
}
