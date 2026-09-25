using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Partner;
using QdratNew.ViewModels.Partner;
using System.Linq;

namespace QdratNew.ViewComponents
{
    public class PartnerSidebarViewComponent : ViewComponent
    {
        private readonly PartnerPermissionService _permissionService;
        private readonly ApplicationDbContext _context;

        public PartnerSidebarViewComponent(
            PartnerPermissionService permissionService,
            ApplicationDbContext context)
        {
            _permissionService = permissionService;
            _context = context;
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
