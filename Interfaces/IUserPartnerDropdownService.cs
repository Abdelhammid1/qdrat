using System.Security.Claims;
using QdratNew.ViewModels.Partner;

namespace QdratNew.Interfaces
{
    public interface IUserPartnerDropdownService
    {
        Task<PartnerDropdownViewModel> GetAsync(
            ClaimsPrincipal user,
            int? activePartnerId
        );
    }
}
