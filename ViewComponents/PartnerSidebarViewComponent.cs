using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.OnlineLecture;
using QdratNew.Services.Partner;
using QdratNew.ViewModels.Partner;
using System.Linq;

namespace QdratNew.ViewComponents
{
    public class PartnerSidebarViewComponent : ViewComponent
    {
        private readonly PartnerPermissionService _permissionService;
        private readonly ApplicationDbContext _context;
        private readonly IPartnerEducationalContentPolicy _educationalContentPolicy; // OLC-S4.3
        private readonly IOnlineLectureFeatureService _onlineLectureFeature;         // OLC-S4.3

        public PartnerSidebarViewComponent(
            PartnerPermissionService permissionService,
            ApplicationDbContext context,
            IPartnerEducationalContentPolicy educationalContentPolicy,
            IOnlineLectureFeatureService onlineLectureFeature)
        {
            _permissionService = permissionService;
            _context = context;
            _educationalContentPolicy = educationalContentPolicy;
            _onlineLectureFeature = onlineLectureFeature;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            int? partnerId = HttpContext.Session.GetInt32("ActivePartnerId");

            if (!partnerId.HasValue)
            {
                return View(new PartnerSidebarPermissionsVM());
            }

            // الصلاحيات (كما هي)
            var vm = await _permissionService.GetActivePermissionsAsync(partnerId.Value);

            // OLC-S4.3 (R19): «المحتوى التعليمي» = صفحة LearningContent للقراءة — تظهر حسب البند (حتى AccessUntilDate)
            // وفقط بعد إطلاق الميزة (OnlineLectureContent.Enabled)
            vm.CanAccessEducationalContent =
                await _onlineLectureFeature.IsEnabledAsync() &&
                await _educationalContentPolicy.CanPartnerReadAsync(partnerId.Value);

            // إضافة لوجو الشريك
            vm.PartnerLogoPath = await _context.Partners
                .AsNoTracking()
                .Where(p => p.Id == partnerId.Value)
                .Select(p => p.LogoPath)
                .FirstOrDefaultAsync();

            return View(vm);
        }
    }
}
