using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QdratNew.Entities;
using QdratNew.Services;

namespace QdratNew.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ImpersonationService _impersonation;

        public LogoutModel(SignInManager<ApplicationUser> signInManager, ImpersonationService impersonation)
        {
            _signInManager = signInManager;
            _impersonation = impersonation;
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            await _signInManager.SignOutAsync();

            // 🧹 إزالة كوكي التخفّي عند تسجيل الخروج حتى لا يؤثر على الجلسة التالية
            _impersonation.Clear();

            if (string.IsNullOrEmpty(returnUrl))
            {
                return Redirect("~/"); // ← هذا يعيدك للصفحة الرئيسية
            }

            return LocalRedirect(returnUrl);
        }

    }
}
