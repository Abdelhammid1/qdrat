using Microsoft.AspNetCore.Mvc;
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

        public IViewComponentResult Invoke()
        {
            int? partnerId = HttpContext.Session.GetInt32("ActivePartnerId");

            if (!partnerId.HasValue)
            {
                return View(new PartnerSidebarPermissionsVM());
            }

            // الصلاحيات (كما هي)
            var vm = _permissionService.GetActivePermissions(partnerId.Value);

            // إضافة لوجو الشريك
            vm.PartnerLogoPath = _context.Partners
                .Where(p => p.Id == partnerId.Value)
                .Select(p => p.LogoPath)
                .FirstOrDefault();

            return View(vm);
        }
    }
}
