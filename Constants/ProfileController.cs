using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Entities;

namespace QdratNew.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // ✅ تحديث الاسم (يُستخدم من البارشال)
        [HttpPost]
        public async Task<IActionResult> UpdateName([FromBody] UpdateNameRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.FullName))
                return Json(new { success = false, message = "الاسم لا يمكن أن يكون فارغًا" });

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { success = false, message = "المستخدم غير موجود" });

            user.FullName = model.FullName.Trim();
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return Json(new
                {
                    success = false,
                    message = "فشل حفظ الاسم"
                });
            }

            return Json(new { success = true });
        }
    }

    // ✅ DTO بسيط
    public class UpdateNameRequest
    {
        public string FullName { get; set; }
    }
}
