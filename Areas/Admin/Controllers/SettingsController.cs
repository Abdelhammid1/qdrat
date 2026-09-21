using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var settings = await _context.SystemSettings
                .OrderBy(s => s.Key)
                .ToListAsync();

            return View(settings);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var setting = await _context.SystemSettings.FindAsync(id);
            if (setting == null) return NotFound();

            return View(setting);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SystemSetting model, IFormFile? ImageFile)
        {
            if (id != model.Id) return BadRequest();

            var setting = await _context.SystemSettings.FindAsync(id);
            if (setting == null) return NotFound();

            // ✅ دعم رفع صورة جديدة للشعار أو الفافيكون
            if ((setting.Key == "SiteLogoPath" || setting.Key == "SiteFaviconPath") && ImageFile != null)
            {
                var fileName = $"{setting.Key}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(ImageFile.FileName)}";
                var savePath = Path.Combine("wwwroot", "uploads", "settings", fileName);

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await ImageFile.CopyToAsync(stream);
                }

                setting.Value = $"/uploads/settings/{fileName}";
            }
            else
            {
                setting.Value = model.Value;
            }

            setting.Description = model.Description;
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم تحديث الإعداد بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        #region 📄 إدارة الصفحات الثابتة

        // 🟩 عرض جميع الصفحات
        [HttpGet]
        public async Task<IActionResult> Pages()
        {
            var pages = await _context.StaticPages.AsNoTracking().ToListAsync();
            return View("Pages", pages);
        }
        [HttpPost]
        [RequestSizeLimit(10_000_000)] // حد أقصى 10 ميجابايت
        public async Task<IActionResult> UploadEditorImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("لم يتم تحديد ملف.");

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var savePath = Path.Combine("wwwroot", "uploads", "editor", fileName);

            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

            using (var stream = new FileStream(savePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var imageUrl = $"/uploads/editor/{fileName}";
            return Json(new { location = imageUrl });
        }

        // 🟦 تعديل صفحة
        [HttpGet]
        public async Task<IActionResult> EditPage(int id)
        {
            var page = await _context.StaticPages.FindAsync(id);
            if (page == null)
                return NotFound();

            return View("EditPage", page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPage(StaticPage model, IFormFile? ImageFile)
        {
            if (!ModelState.IsValid)
                return View("EditPage", model);

            var page = await _context.StaticPages.FindAsync(model.Id);
            if (page == null)
                return NotFound();

            page.Title = model.Title;
            page.Slug = model.Slug;
            page.Content = model.Content;
            page.IsActive = model.IsActive;

            // ✅ رفع الصورة الجديدة إن وُجدت
            if (ImageFile != null && ImageFile.Length > 0)
            {
                // تأكد من وجود المجلد
                var uploadDir = Path.Combine("wwwroot", "uploads", "pages");
                Directory.CreateDirectory(uploadDir);

                // اسم فريد للصورة
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);

                // حفظ الملف
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await ImageFile.CopyToAsync(stream);
                }

                // تحديث المسار في قاعدة البيانات
                page.ImagePath = $"/uploads/pages/{fileName}";
            }

            _context.Update(page);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم حفظ التعديلات بنجاح.";
            return RedirectToAction(nameof(Pages));
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePageImage(int id)
        {
            var page = await _context.StaticPages.FindAsync(id);
            if (page == null)
                return NotFound();

            if (!string.IsNullOrEmpty(page.ImagePath))
            {
                // حذف الصورة من المجلد
                var filePath = Path.Combine("wwwroot", page.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // إزالة المسار من قاعدة البيانات
                page.ImagePath = null;
                await _context.SaveChangesAsync();

                TempData["Success"] = "🗑️ تم حذف صورة الغلاف بنجاح.";
            }
            else
            {
                TempData["Error"] = "⚠️ لا توجد صورة لحذفها.";
            }

            return RedirectToAction(nameof(EditPage), new { id });
        }


        // ✅ عرض نموذج إنشاء صفحة جديدة
        [HttpGet]
        public IActionResult CreatePage()
        {
            var newPage = new StaticPage
            {
                IsActive = true
            };
            return View("CreatePage", newPage);
        }



        [HttpGet]
        public async Task<IActionResult> CheckSlugExists(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return Json(new { exists = false });

            bool exists = await _context.StaticPages
                .AnyAsync(p => p.Slug.ToLower() == slug.ToLower());

            return Json(new { exists });
        }



        // ✅ حفظ الصفحة الجديدة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePage(StaticPage model, IFormFile? ImageFile)
        {
            if (!ModelState.IsValid)
                return View("CreatePage", model);

            // 🟩 حفظ الصورة إن وُجدت
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var uploadDir = Path.Combine("wwwroot", "uploads", "pages");
                Directory.CreateDirectory(uploadDir);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await ImageFile.CopyToAsync(stream);
                }

                model.ImagePath = $"/uploads/pages/{fileName}";
            }

            _context.StaticPages.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم إنشاء الصفحة بنجاح.";
            return RedirectToAction(nameof(Pages));
        }




        #endregion



    }
}
