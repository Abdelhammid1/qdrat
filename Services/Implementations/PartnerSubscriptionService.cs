using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
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
    }
}
