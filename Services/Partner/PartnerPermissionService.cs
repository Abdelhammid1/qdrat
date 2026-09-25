using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Partner;

namespace QdratNew.Services.Partner
{
    public class PartnerPermissionService
    {
        private readonly ApplicationDbContext _context;

        public PartnerPermissionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public PartnerSidebarPermissionsVM GetActivePermissions(int partnerId)
        {
            var now = DateTime.Now;

            var sub = _context.PartnerSubscriptions
                .FirstOrDefault(x =>
                    x.PartnerId == partnerId &&
                    x.StartDate <= now &&
                    x.EndDate >= now);

            return MapPermissions(sub);
        }

        public async Task<PartnerSidebarPermissionsVM> GetActivePermissionsAsync(int partnerId)
        {
            var now = DateTime.Now; // نفس السلوك الحالي

            var sub = await _context.PartnerSubscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.PartnerId == partnerId &&
                    x.StartDate <= now &&
                    x.EndDate >= now);

            return MapPermissions(sub);
        }

        private static PartnerSidebarPermissionsVM MapPermissions(PartnerSubscription? sub)
        {
            if (sub == null)
                return new PartnerSidebarPermissionsVM();

            return new PartnerSidebarPermissionsVM
            {
                CanCreateHomework = sub.CanCreateHomework,
                CanCreateExams = sub.CanCreateExams,

                CanUsePlacementExams = sub.CanUsePlacementExams,
                CanUsePerformanceIndicatorExams = sub.CanUsePerformanceIndicatorExams,

                CanUseReinforcementSkills = sub.CanUseReinforcementSkills,
                CanUseRemedialPlans = sub.CanUseRemedialPlans,
                CanUseRemedialSessions = sub.CanUseRemedialSessions,

                CanAccessEducationalContent = sub.CanAccessEducationalContent,
                CanUseProfessionalModels = sub.CanUseProfessionalModels,

                // 🔴 إضافة إدارة المدربين
                CanManageInstructors =
                    sub.MaxInstructors.HasValue &&
                    sub.MaxInstructors.Value > 0
            };
        }


    }

}
