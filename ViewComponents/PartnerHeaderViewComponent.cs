using Microsoft.AspNetCore.Mvc;
using QdratNew.Interfaces;

namespace QdratNew.ViewComponents
{
    public class PartnerHeaderViewComponent : ViewComponent
    {
        private readonly IUserPartnerDropdownService _partnerDropdownService;

        public PartnerHeaderViewComponent(
            IUserPartnerDropdownService partnerDropdownService)
        {
            _partnerDropdownService = partnerDropdownService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var vm = await _partnerDropdownService.GetAsync(
                HttpContext.User, // ✅ ClaimsPrincipal
                HttpContext.Session.GetInt32("ActivePartnerId")
            );

            return View(vm);
        }
    }
}
