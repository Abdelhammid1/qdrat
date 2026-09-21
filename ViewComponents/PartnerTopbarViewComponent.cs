using Microsoft.AspNetCore.Mvc;
using QdratNew.Data;
using QdratNew.ViewModels.Partner;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QdratNew.ViewComponents
{
    public class PartnerTopbarViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public PartnerTopbarViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = UserClaimsPrincipal
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            int? activePartnerId =
                HttpContext.Session.GetInt32("ActivePartnerId");

            // جميع المدارس المرتبطة بالمستخدم
            var partners = _context.UserPartners
                .Where(up => up.UserId == userId)
                .Select(up => new PartnerItemVM
                {
                    Id = up.Partner.Id,
                    Name = up.Partner.Name,
                    LogoPath = up.Partner.LogoPath
                })
                .OrderBy(p => p.Name)
                .ToList();

            var activePartner = partners
                .FirstOrDefault(p => p.Id == activePartnerId);

            var vm = new PartnerTopbarVM
            {
                ActivePartnerId = activePartner?.Id ?? 0,
                ActivePartnerName = activePartner?.Name,
                ActivePartnerLogo = activePartner?.LogoPath,
                Partners = partners
            };

            return View(vm);
        }
    }
}
