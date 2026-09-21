using Microsoft.AspNetCore.Identity;
using QdratNew.Entities;
using QdratNew.Interfaces;
using QdratNew.ViewModels.Users;
using System.Security.Claims;

public class UserAccountDropdownService : IUserAccountDropdownService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserAccountDropdownService(UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<UserAccountDropdownViewModel> GetDropdownViewModelAsync(ClaimsPrincipal user)
    {
        // نحصل على المستخدم من نوع ApplicationUser
        var appUser = await _userManager.GetUserAsync(user) as ApplicationUser;
        if (appUser == null) return null;

        // تحقق آمن من صورة المستخدم
        var imagePath = string.IsNullOrWhiteSpace(appUser.ProfileImagePath)
            ? "/images/avatar-male.png"
            : appUser.ProfileImagePath;

        // الأدوار
        var roles = (await _userManager.GetRolesAsync(appUser)).ToList();
        var selectedRole = _httpContextAccessor.HttpContext?.Session.GetString("ActiveRole") ?? roles.FirstOrDefault();

        return new UserAccountDropdownViewModel
        {
            Email = appUser.Email ?? "no-email@domain.com",
            FullName = appUser.FullName ?? appUser.UserName ?? "مستخدم",
            EditNameUrl = "/Profile/Edit",
            Roles = roles,
            SelectedRole = selectedRole,
            ProfileImageUrl = imagePath,
            Courses = new List<UserCourseOption>(), // مؤقتًا
            SelectedCourseId = _httpContextAccessor.HttpContext?.Session.GetInt32("ActiveCourseId"),
            LogoutUrl = "/Account/Logout"
        };
    }
}
