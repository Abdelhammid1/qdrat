using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities.Frontend;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]

    public class FrontendCoursesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public FrontendCoursesController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // 🟢 عرض جميع الدورات
        public async Task<IActionResult> Index()
        {
            var courses = await _context.FrontendCourses
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
            return View(courses);
        }

        // ====================== 🟢 إنشاء دورة جديدة ======================
        public IActionResult Create()
        {
            // تمرير كائن فارغ لتفادي NullReference
            return View(new FrontendCourse());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FrontendCourse model, IFormFile? imageFile)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    TempData["Error"] = "⚠️ يجب إدخال عنوان الدورة.";
                    return View(model);
                }
                model.Slug = string.IsNullOrWhiteSpace(model.Slug)
                    ? model.Title
                        .Trim()
                        .ToLower()
                        .Replace(" ", "-")
                        .Replace("ـ", "-")
                        .Replace("،", "")
                        .Replace(".", "")
                        .Replace(":", "")
                    : model.Slug.Trim().ToLower();


                bool slugExists = await _context.FrontendCourses.AnyAsync(c => c.Slug == model.Slug);
                if (slugExists)
                {
                    TempData["Error"] = "⚠️ هذا الرابط مستخدم مسبقًا.";
                    return View(model);
                }

                if (imageFile == null || imageFile.Length == 0)
                {
                    TempData["Error"] = "⚠️ يجب رفع صورة الغلاف.";
                    return View(model);
                }

                // 🟢 رفع الصورة بصيغة WebP
                var folder = Path.Combine(_env.WebRootPath, "uploads", "courses");
                Directory.CreateDirectory(folder);

                var fileName = $"{Guid.NewGuid()}.webp";
                var fullPath = Path.Combine(folder, fileName);

                using (var image = await SixLabors.ImageSharp.Image.LoadAsync(imageFile.OpenReadStream()))
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new SixLabors.ImageSharp.Size(1200, 0),
                        Mode = ResizeMode.Max
                    }));

                    await image.SaveAsync(fullPath, new WebpEncoder
                    {
                        Quality = 75
                    });
                }

                model.ImagePath = "/uploads/courses/" + fileName;
                model.CreatedAt = DateTime.Now;
                model.IsActive = true;

                _context.FrontendCourses.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "✅ تم إنشاء الدورة بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "❌ خطأ أثناء الإنشاء: " + ex.Message;
                return View(model);
            }
        }

        // ====================== 🟡 التحقق من وجود Slug ======================
        [HttpGet]
        public async Task<JsonResult> CheckSlugExists(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return Json(new { exists = false });

            bool exists = await _context.FrontendCourses.AnyAsync(c => c.Slug == slug);
            return Json(new { exists });
        }

        // ====================== 🟠 تعديل دورة ======================
        public async Task<IActionResult> Edit(int id)
        {
            var course = await _context.FrontendCourses.FindAsync(id);
            if (course == null)
                return NotFound();

            return View(course);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FrontendCourse model, IFormFile? imageFile)
        {
            try
            {
                var course = await _context.FrontendCourses.FindAsync(id);
                if (course == null)
                    return NotFound();

                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    TempData["Error"] = "⚠️ يجب إدخال عنوان الدورة.";
                    return View(model);
                }

                model.Slug = string.IsNullOrWhiteSpace(model.Slug)
       ? model.Title
           .Trim()
           .ToLower()
           .Replace(" ", "-")
           .Replace("ـ", "-")
           .Replace("،", "")
           .Replace(".", "")
           .Replace(":", "")
       : model.Slug.Trim().ToLower();


                bool slugExists = await _context.FrontendCourses
                    .AnyAsync(c => c.Slug == model.Slug && c.Id != id);

                if (slugExists)
                {
                    TempData["Error"] = "⚠️ هذا الرابط مستخدم مسبقًا.";
                    return View(model);
                }

                // تحديث البيانات
                course.Title = model.Title;
                course.Slug = model.Slug;
                course.ShortDescription = model.ShortDescription;
                course.FullDescription = model.FullDescription;
                course.ShowInNavbar = model.ShowInNavbar;
                course.ShowAsMainLink = model.ShowAsMainLink;
                course.DisplayOrder = model.DisplayOrder;
                course.IsActive = model.IsActive;

                // 🟢 تحديث الصورة
                if (imageFile != null && imageFile.Length > 0)
                {
                    var folder = Path.Combine(_env.WebRootPath, "uploads", "courses");
                    Directory.CreateDirectory(folder);

                    // حذف الصورة القديمة
                    if (!string.IsNullOrEmpty(course.ImagePath))
                    {
                        var oldPath = Path.Combine(
                            _env.WebRootPath,
                            course.ImagePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
                        );

                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
                    }

                    var fileName = $"{Guid.NewGuid()}.webp";
                    var fullPath = Path.Combine(folder, fileName);

                    using (var image = await SixLabors.ImageSharp.Image.LoadAsync(imageFile.OpenReadStream()))
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Size = new SixLabors.ImageSharp.Size(1200, 0),
                            Mode = ResizeMode.Max
                        }));

                        await image.SaveAsync(fullPath, new WebpEncoder
                        {
                            Quality = 75
                        });
                    }

                    course.ImagePath = "/uploads/courses/" + fileName;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "✅ تم تعديل الدورة بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "❌ خطأ أثناء التعديل: " + ex.Message;
                return View(model);
            }
        }

        // 🔴 حذف
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var course = await _context.FrontendCourses.FindAsync(id);
            if (course == null) return NotFound();

            _context.FrontendCourses.Remove(course);
            await _context.SaveChangesAsync();

            TempData["Success"] = "🗑️ تم حذف الدورة بنجاح";
            return RedirectToAction(nameof(Index));
        }











    }
}
