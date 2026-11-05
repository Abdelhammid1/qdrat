using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Interfaces;
using QdratNew.ViewModels.Partner;

namespace QdratNew.Implementations
{
    public class PartnerSubscriptionService : IPartnerSubscriptionService
    {
        private readonly ApplicationDbContext _context;

        public PartnerSubscriptionService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // 🔹 الاشتراك النشط
        // =====================================================
        public PartnerSubscription? GetActiveSubscription(int partnerId)
        {
            var today = DateTime.Today;

            return _context.PartnerSubscriptions
                .Where(s =>
                    s.PartnerId == partnerId &&
                    s.StartDate <= today &&
                    s.EndDate >= today)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefault();
        }




        // =====================================================
        // 🔹 فترة الاشتراك النشطة
        // =====================================================
        public PartnerSubscriptionPeriod? GetActivePeriod(int partnerId)
        {
            var today = DateTime.Today;

            return _context.PartnerSubscriptionPeriods
                .Where(p =>
                    p.PartnerSubscription.PartnerId == partnerId &&
                    p.StartDate <= today &&
                    p.EndDate >= today
                )
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefault();
        }

        // =====================================================
        // 🔹 جميع فترات الاشتراك للشريك
        // =====================================================
        public IQueryable<PartnerSubscriptionPeriod> GetAllPeriods(int partnerId)
        {
            return _context.PartnerSubscriptionPeriods
                .Where(p => p.PartnerSubscription.PartnerId == partnerId);
        }

        // =====================================================
        // 🔹 الطلاب
        // =====================================================
        public bool CanAddStudents(int partnerId, int studentsToAdd = 1)
        {
            var sub = GetActiveSubscription(partnerId);

            if (sub == null)
                return false;

            if (!sub.MaxActiveStudents.HasValue)
                return true;

            var activePeriod = GetActivePeriod(partnerId);
            if (activePeriod == null)
                return false;

            var activeStudentsCount = _context.Students
                .Count(s => s.PartnerSubscriptionPeriodId == activePeriod.Id);


            return (activeStudentsCount + studentsToAdd)
                   <= sub.MaxActiveStudents.Value;
        }

        // =====================================================
        // 🔹 واجبات واختبارات
        // =====================================================
        public bool CanUseHomework(int partnerId)
        {
            var sub = GetActiveSubscription(partnerId);
            return sub != null && sub.CanCreateHomework;
        }

        public bool CanUseExams(int partnerId)
        {
            var sub = GetActiveSubscription(partnerId);
            return sub != null && sub.CanCreateExams;
        }

        // =====================================================
        // 🔹 اختبارات متقدمة
        // =====================================================
        public bool CanUsePlacementExams(int partnerId)
        {
            var sub = GetActiveSubscription(partnerId);
            return sub != null && sub.CanUsePlacementExams;
        }

        public bool CanUsePerformanceIndicatorExams(int partnerId)
        {
            var sub = GetActiveSubscription(partnerId);
            return sub != null && sub.CanUsePerformanceIndicatorExams;
        }

        // APD-S1.3
        public bool CanUseMinistrySimExams(int partnerId)
        {
            var sub = GetActiveSubscription(partnerId);
            return sub != null && sub.CanUseMinistrySimExams;
        }

        // =====================================================
        // 🔹 الخطط العلاجية والمهارات
        // =====================================================
        public bool CanUseRemedialPlans(int partnerId)
        {
            var sub = GetActiveSubscription(partnerId);
            return sub != null && sub.CanUseRemedialPlans;
        }

        public bool CanUseRemedialSessions(int partnerId)
        {
            var sub = GetActiveSubscription(partnerId);
            return sub != null && sub.CanUseRemedialSessions;
        }

        public bool CanUseReinforcementSkills(int partnerId)
        {
            var sub = GetActiveSubscription(partnerId);
            return sub != null && sub.CanUseReinforcementSkills;
        }

        // =====================================================
        // 🔹 المحتوى التعليمي
        // =====================================================
        public bool CanAccessEducationalContent(int partnerId)
        {
            var sub = GetActiveSubscription(partnerId);
            return sub != null && sub.CanAccessEducationalContent;
        }

        // =====================================================
        // 🔹 الذكاء الاصطناعي
        // =====================================================
        public bool CanUseAIAnalytics(int partnerId)
        {
            var sub = GetActiveSubscription(partnerId);
            return sub != null && sub.CanUseAIAnalytics;
        }

        // =====================================================
        // 🔹 النماذج الاحترافية (PPL-S1.3)
        // =====================================================
        public bool CanUseProfessionalModels(int partnerId)
        {
            var sub = GetActiveSubscription(partnerId);
            return sub != null && sub.CanUseProfessionalModels;
        }

        // =====================================================
        // 🔹 بنك الأسئلة والفروع
        // =====================================================
        public bool CanUseQuestionBank(int partnerId)
        {
            var sub = GetActiveSubscription(partnerId);
            return sub != null && sub.CanUseQuestionBank;
        }

        public bool CanCreateMultipleBranches(int partnerId)
        {
            var sub = GetActiveSubscription(partnerId);
            return sub != null && sub.AllowMultipleBranches;
        }



        public PartnerSubscriptionContext? GetActiveContext(int partnerId)
        {
            var today = DateTime.Today;

            var subscription = _context.PartnerSubscriptions
                .Include(s => s.SubscriptionCourses)
                    .ThenInclude(sc => sc.Course)
                .FirstOrDefault(s =>
                    s.PartnerId == partnerId &&
                    s.StartDate <= today &&
                    s.EndDate >= today);

            if (subscription == null)
                return null;

            var period = _context.PartnerSubscriptionPeriods
                .FirstOrDefault(p =>
                    p.PartnerSubscriptionId == subscription.Id &&
                    p.StartDate <= today &&
                    p.EndDate >= today);

            if (period == null)
                return null;

            return new PartnerSubscriptionContext
            {
                PartnerId = partnerId,
                SubscriptionId = subscription.Id,
                ActivePeriodId = period.Id,

                // 🔐 الصلاحيات (النقطة الحاسمة)
                CanCreateHomework = subscription.CanCreateHomework,
                CanCreateExams = subscription.CanCreateExams,

                CanUsePlacementExams = subscription.CanUsePlacementExams,
                CanUsePerformanceIndicatorExams = subscription.CanUsePerformanceIndicatorExams,
                CanUseMinistrySimExams = subscription.CanUseMinistrySimExams,

                CanUseReinforcementSkills = subscription.CanUseReinforcementSkills,
                CanUseRemedialPlans = subscription.CanUseRemedialPlans,
                CanUseRemedialSessions = subscription.CanUseRemedialSessions,

                CanAccessEducationalContent = subscription.CanAccessEducationalContent,
                CanUseQuestionBank = subscription.CanUseQuestionBank,
                CanCreateMultipleBranches = subscription.AllowMultipleBranches,

                CanUseProfessionalModels = subscription.CanUseProfessionalModels,
                CanUseAIAnalytics = subscription.CanUseAIAnalytics,

                Courses = subscription.SubscriptionCourses
                    .Select(sc => new PartnerCourseContext
                    {
                        CourseId = sc.CourseId,
                        CourseName = sc.Course.Name,
                        CanUsePlatformQuestionBank = sc.CanUsePlatformQuestionBank
                    })
                    .ToList()
            };
        }


        // =====================================================
        // 🔹 الوصول للبيانات بعد انتهاء العقد
        // =====================================================
        public bool HasDataAccess(int partnerId)
        {
            var today = DateTime.Today;

            var sub = _context.PartnerSubscriptions
                .Where(s => s.PartnerId == partnerId)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefault();

            if (sub == null)
                return false;

            if (sub.IsActive)
                return true;

            return sub.AccessUntilDate.HasValue &&
                   today <= sub.AccessUntilDate.Value;
        }

        // PPL-S6: نفس قرار PartnerFeatureFilter مع AllowReadAfterExpiry، لإظهار الروابط
        public bool CanReadPlacementReports(int partnerId)
        {
            var active = GetActiveSubscription(partnerId);

            return active != null
                ? active.CanUsePlacementExams
                : CanReadFeatureAfterExpiry(partnerId, PartnerFeatureType.PlacementExams);
        }

        public bool CanReadFeatureAfterExpiry(int partnerId, PartnerFeatureType feature)
        {
            var today = DateTime.Today;

            // عقد لم يبدأ بعد لا يحدد صلاحية القراءة الآن
            var sub = _context.PartnerSubscriptions
                .Where(s => s.PartnerId == partnerId && s.StartDate <= today)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefault();

            if (sub == null || !IncludesFeature(sub, feature))
                return false;

            return sub.IsActive ||
                   (sub.AccessUntilDate.HasValue && today <= sub.AccessUntilDate.Value);
        }

        private static bool IncludesFeature(PartnerSubscription sub, PartnerFeatureType feature) => feature switch
        {
            PartnerFeatureType.Homework => sub.CanCreateHomework,
            PartnerFeatureType.Exams => sub.CanCreateExams,
            PartnerFeatureType.PlacementExams => sub.CanUsePlacementExams,
            PartnerFeatureType.PerformanceIndicatorExams => sub.CanUsePerformanceIndicatorExams,
            PartnerFeatureType.ReinforcementSkills => sub.CanUseReinforcementSkills,
            PartnerFeatureType.RemedialPlans => sub.CanUseRemedialPlans,
            PartnerFeatureType.RemedialSessions => sub.CanUseRemedialSessions,
            PartnerFeatureType.EducationalContent => sub.CanAccessEducationalContent,
            PartnerFeatureType.AIAnalytics => sub.CanUseAIAnalytics,
            PartnerFeatureType.ProfessionalModels => sub.CanUseProfessionalModels,
            PartnerFeatureType.MinistrySimExams => sub.CanUseMinistrySimExams,
            _ => false
        };
    }
}
