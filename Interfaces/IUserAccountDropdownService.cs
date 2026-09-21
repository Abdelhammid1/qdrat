using QdratNew.ViewModels.Users;
using System.Security.Claims;


namespace QdratNew.Interfaces { 
public interface IUserAccountDropdownService
{
    Task<UserAccountDropdownViewModel> GetDropdownViewModelAsync(ClaimsPrincipal user);
}
}