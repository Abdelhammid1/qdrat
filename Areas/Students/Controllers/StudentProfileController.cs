using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Students;

namespace QdratNew.Areas.Students.Controllers
{
    public class StudentProfileController : StudentBaseController
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public StudentProfileController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            SignInManager<ApplicationUser> signInManager)
            : base(contextFactory, userManager)
        {
            _webHostEnvironment = webHostEnvironment;
            _signInManager = signInManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account", new { area = "" });

            var vm = new StudentProfileEditVm
            {
                FullName = user.FullName ?? string.Empty,
                ProfileImagePath = user.ProfileImagePath,
                LastNameChangeDate = user.LastNameChangeDate
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateDisplayName([FromBody] UpdateDisplayNameRequest request)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "البيانات غير صالحة." });

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { success = false, message = "المستخدم غير موجود." });

            if (user.LastNameChangeDate.HasValue && user.LastNameChangeDate.Value.AddDays(7) > DateTime.Now)
            {
                var nextChange = user.LastNameChangeDate.Value.AddDays(7).ToString("yyyy/MM/dd");
                return Json(new { success = false, message = $"لا يمكنك تعديل الاسم إلا بعد {nextChange}" });
            }

            user.FullName = request.FullName.Trim();
            user.LastNameChangeDate = DateTime.Now;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return Json(new { success = false, message = "حدث خطأ أثناء حفظ الاسم." });

            return Json(new { success = true, message = "تم تحديث الاسم بنجاح.", newName = user.FullName });
        }

        [HttpPost]
        public async Task<IActionResult> UploadProfileImage(IFormFile profileImage)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { success = false, message = "المستخدم غير موجود." });

            if (profileImage == null || profileImage.Length == 0)
                return Json(new { success = false, message = "لم يتم اختيار صورة." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(profileImage.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return Json(new { success = false, message = "يُسمح فقط بصور JPG أو PNG أو WEBP." });

            if (profileImage.Length > 5 * 1024 * 1024)
                return Json(new { success = false, message = "حجم الصورة يجب ألا يتجاوز 5 ميجابايت." });

            var profilesDir = Path.Combine(_webHostEnvironment.WebRootPath, "images", "profiles");
            Directory.CreateDirectory(profilesDir);

            var fileName = Guid.NewGuid() + ext;
            var filePath = Path.Combine(profilesDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await profileImage.CopyToAsync(stream);
            }

            // حذف الصورة القديمة إن وجدت
            if (!string.IsNullOrEmpty(user.ProfileImagePath))
            {
                var oldFile = Path.Combine(_webHostEnvironment.WebRootPath, user.ProfileImagePath.TrimStart('/'));
                if (System.IO.File.Exists(oldFile))
                    System.IO.File.Delete(oldFile);
            }

            user.ProfileImagePath = $"/images/profiles/{fileName}";
            await _userManager.UpdateAsync(user);

            return Json(new { success = true, message = "تم تحديث الصورة بنجاح.", imageUrl = user.ProfileImagePath });
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault();
                return Json(new { success = false, message = errors ?? "البيانات غير صالحة." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { success = false, message = "المستخدم غير موجود." });

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                var error = result.Errors.FirstOrDefault()?.Description ?? "حدث خطأ أثناء تغيير كلمة المرور.";
                // ترجمة رسائل الخطأ الشائعة
                if (error.Contains("Incorrect password") || error.Contains("PasswordMismatch"))
                    error = "كلمة المرور الحالية غير صحيحة.";
                else if (error.Contains("too short") || error.Contains("PasswordTooShort"))
                    error = "كلمة المرور الجديدة قصيرة جداً.";

                return Json(new { success = false, message = error });
            }

            await _signInManager.RefreshSignInAsync(user);
            return Json(new { success = true, message = "تم تغيير كلمة المرور بنجاح." });
        }
    }
}
