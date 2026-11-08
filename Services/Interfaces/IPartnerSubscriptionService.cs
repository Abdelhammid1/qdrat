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
        bool CanUseMinistrySimExams(int partnerId); // APD-S1.3

        // =========================================
        // 🔹 الخطط العلاجية والمهارات
        // =========================================
        bool CanUseRemedialPlans(int partnerId);

        bool CanUseRemedialSessions(int partnerId);

        bool CanUseReinforcementSkills(int partnerId);
        bool CanUseAIAnalytics(int partnerId);
        bool CanUseProfessionalModels(int partnerId); // PPL-S1.3



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

        // OLC-S4.1: النسخة async — forStudents=false ⇒ عقد نشط الآن يشمل البند (R16)،
        // forStudents=true ⇒ البند + العقد ما زال ساريًا للطلاب حتى AccessUntilDate ?? EndDate (R17)
        Task<bool> CanAccessEducationalContentAsync(int partnerId, bool forStudents, CancellationToken ct = default);

        // =========================================
        // 🔹 صلاحيات عامة
        // =========================================
        bool CanUseQuestionBank(int partnerId);

        bool CanCreateMultipleBranches(int partnerId);

        // =========================================
        // 🔹 الوصول للبيانات
        // =========================================
        bool HasDataAccess(int partnerId);

        // PPL-S6: عقد نشط يشمل الميزة، أو لا عقد نشط ومازالت القراءة مسموحة حتى AccessUntilDate
        bool CanReadPlacementReports(int partnerId);

        // PPL-S6: آخر عقد بدأ يشمل الميزة، وما زال نشطًا أو ضمن AccessUntilDate
        bool CanReadFeatureAfterExpiry(int partnerId, QdratNew.Enums.PartnerFeatureType feature);
    }
}
