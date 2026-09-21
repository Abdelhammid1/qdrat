using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Entities;
using QdratNew.ViewModels.Users;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ProfileController(UserManager<ApplicationUser> userManager, IWebHostEnvironment webHostEnvironment)
    {
        _userManager = userManager;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    public async Task<IActionResult> EditName()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var model = new EditNameViewModel
        {
            FullName = user.FullName,
            LastNameChangeDate = user.LastNameChangeDate
        };

        return View(model);
    }


    [HttpPost]
    public async Task<IActionResult> UpdateName([FromBody] EditNameAjaxModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || string.IsNullOrWhiteSpace(model.FullName))
            return Json(new { success = false, message = "اسم غير صالح." });

        if (user.LastNameChangeDate.HasValue && user.LastNameChangeDate.Value.AddDays(7) > DateTime.Now)
        {
            var nextChange = user.LastNameChangeDate.Value.AddDays(7).ToString("yyyy/MM/dd");
            return Json(new { success = false, message = $"لا يمكنك تعديل الاسم إلا بعد {nextChange}" });
        }

        user.FullName = model.FullName.Trim();
        user.LastNameChangeDate = DateTime.Now;

        await _userManager.UpdateAsync(user);
        return Json(new { success = true });
    }




    [HttpPost]
    public async Task<IActionResult> EditName(EditNameViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        if (!model.IsAllowedToChange)
        {
            TempData["Error"] = "لا يمكنك تعديل الاسم قبل مرور 7 أيام.";
            return RedirectToAction(nameof(EditName));
        }

        if (!ModelState.IsValid) return View(model);

        user.FullName = model.FullName;
        user.LastNameChangeDate = DateTime.Now;

        await _userManager.UpdateAsync(user);

        TempData["Success"] = "تم تغيير الاسم بنجاح.";
        return RedirectToAction(nameof(EditName));
    }

    [HttpPost]
    public async Task<IActionResult> UploadProfileImage(IFormFile profileImage)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || profileImage == null) return NotFound();

        var fileName = Guid.NewGuid() + Path.GetExtension(profileImage.FileName);
        var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "profiles", fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await profileImage.CopyToAsync(stream);
        }

        user.ProfileImagePath = $"/images/profiles/{fileName}";
        await _userManager.UpdateAsync(user);

        TempData["Success"] = "تم تحديث الصورة.";
        return RedirectToAction(nameof(EditName));
    }
}
