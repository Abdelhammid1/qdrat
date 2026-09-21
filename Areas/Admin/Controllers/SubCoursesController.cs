using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities.Frontend;
using QdratNew.Helpers;
using SixLabors.ImageSharp.Formats.Webp;
using System.Text;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class SubCoursesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SubCoursesController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // 📋 عرض جميع الدورات الفرعية
        public async Task<IActionResult> Index()
        {
            var list = await _context.SubCourses
                .Include(s => s.FrontendCourse)
                .OrderBy(s => s.FrontendCourse.Title)
                .ThenBy(s => s.DisplayOrder)
                .ToListAsync();

            return View(list);
        }

        // 🟢 إنشاء دورة فرعية جديدة
        public IActionResult Create()
        {
            ViewBag.Courses = new SelectList(_context.FrontendCourses.Where(c => c.IsActive), "Id", "Title");
            return View(new SubCourse());
        }

        // ✅ إنشاء دورة فرعية جديدة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubCourse model, IFormFile? imageFile)
        {
            try
            {
                // 🟢 التحقق من الدورة الرئيسية
                if (model.FrontendCourseId == 0 ||
                    !_context.FrontendCourses.Any(c => c.Id == model.FrontendCourseId))
                {
                    TempData["Error"] = "⚠️ يجب اختيار الدورة الرئيسية.";
                    ViewBag.Courses = new SelectList(
                        _context.FrontendCourses.Where(c => c.IsActive).OrderBy(c => c.Title),
                        "Id", "Title", model.FrontendCourseId
                    );
                    return View(model);
                }

                // 🟢 التحقق من العنوان
                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    TempData["Error"] = "⚠️ يجب إدخال عنوان الدورة الفرعية.";
                    ViewBag.Courses = new SelectList(
                        _context.FrontendCourses.Where(c => c.IsActive).OrderBy(c => c.Title),
                        "Id", "Title", model.FrontendCourseId
                    );
                    return View(model);
                }

                // 🟢 توليد Slug
                model.Slug = string.IsNullOrWhiteSpace(model.Slug)
                    ? GenerateSlug(model.Title)
                    : model.Slug;

                // 🟢 منع تكرار الـ Slug
                bool slugExists = await _context.SubCourses.AnyAsync(s => s.Slug == model.Slug);
                if (slugExists)
                {
                    TempData["Error"] = "⚠️ هذا الرابط مستخدم مسبقًا.";
                    ViewBag.Courses = new SelectList(
                        _context.FrontendCourses.Where(c => c.IsActive).OrderBy(c => c.Title),
                        "Id", "Title", model.FrontendCourseId
                    );
                    return View(model);
                }

                // 🟢 رفع الصورة (WebP)
                if (imageFile != null && imageFile.Length > 0)
                {
                    var folder = Path.Combine(_env.WebRootPath, "uploads", "subcourses");
                    Directory.CreateDirectory(folder);

                    var fileName = $"{Guid.NewGuid()}.webp";
                    var fullPath = Path.Combine(folder, fileName);

                    using (var image = await Image.LoadAsync(imageFile.OpenReadStream()))
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Size = new Size(1200, 0),
                            Mode = ResizeMode.Max
                        }));

                        await image.SaveAsync(fullPath, new WebpEncoder
                        {
                            Quality = 75
                        });
                    }

                    model.ImagePath = "/uploads/subcourses/" + fileName;
                }

                // 🟢 بيانات افتراضية
                model.CreatedAt = DateTime.Now;

                _context.SubCourses.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"✅ تم إنشاء الدورة الفرعية بنجاح: {model.Title}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "❌ خطأ أثناء الإنشاء: " + ex.Message;
                ViewBag.Courses = new SelectList(
                    _context.FrontendCourses.Where(c => c.IsActive).OrderBy(c => c.Title),
                    "Id", "Title", model.FrontendCourseId
                );
                return View(model);
            }
        }

        // ✏️ تعديل دورة فرعية
        public async Task<IActionResult> Edit(int id)
        {
            var sub = await _context.SubCourses.FindAsync(id);
            if (sub == null) return NotFound();

            ViewBag.Courses = new SelectList(_context.FrontendCourses, "Id", "Title", sub.FrontendCourseId);
            return View(sub);
        }

        // ✏️ تعديل دورة فرعية
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SubCourse model, IFormFile? imageFile)
        {
            try
            {
                var sub = await _context.SubCourses.FindAsync(id);
                if (sub == null)
                    return NotFound();

                // 🟢 التحقق من العنوان
                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    TempData["Error"] = "⚠️ يجب إدخال عنوان الدورة الفرعية.";
                    ViewBag.Courses = new SelectList(_context.FrontendCourses.Where(c => c.IsActive).OrderBy(c => c.Title), "Id", "Title", model.FrontendCourseId);
                    return View(model);
                }

                // 🟢 التحقق وتحديث الـ Slug
                if (string.IsNullOrWhiteSpace(model.Slug))
                {
                    sub.Slug = GenerateSlug(model.Title);
                }
                else if (!string.Equals(model.Slug, sub.Slug, StringComparison.OrdinalIgnoreCase))
                {
                    bool slugExists = await _context.SubCourses
                        .AnyAsync(s => s.Slug == model.Slug && s.Id != id);

                    if (slugExists)
                    {
                        TempData["Error"] = "⚠️ هذا الرابط مستخدم مسبقًا. يرجى اختيار رابط آخر.";
                        ViewBag.Courses = new SelectList(_context.FrontendCourses.Where(c => c.IsActive).OrderBy(c => c.Title), "Id", "Title", model.FrontendCourseId);
                        return View(model);
                    }

                    sub.Slug = model.Slug;
                }

                // 🟢 تحديث باقي الحقول
                sub.Title = model.Title;
                sub.ShortDescription = model.ShortDescription;
                sub.FullDescription = model.FullDescription;
                sub.DisplayOrder = model.DisplayOrder;
                sub.ShowOnHomePage = model.ShowOnHomePage;
                sub.IsActive = model.IsActive;
                sub.FrontendCourseId = model.FrontendCourseId;

                // 🟢 تحديث الصورة
            
                if (imageFile != null && imageFile.Length > 0)
                {
                    var folder = Path.Combine(_env.WebRootPath, "uploads", "subcourses");
                    Directory.CreateDirectory(folder);

                    // 🟡 حذف الصورة القديمة إن وُجدت
                    if (!string.IsNullOrEmpty(sub.ImagePath))
                    {
                        var oldPath = Path.Combine(
                            _env.WebRootPath,
                            sub.ImagePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
                        );

                        if (System.IO.File.Exists(oldPath))
                        {
                            System.IO.File.Delete(oldPath);
                        }
                    }

                    // 🟢 إنشاء اسم جديد بصيغة WebP
                    var fileName = $"{Guid.NewGuid()}.webp";
                    var fullPath = Path.Combine(folder, fileName);

                    // 🟢 تحميل وتحويل الصورة
                    using (var image = await Image.LoadAsync(imageFile.OpenReadStream()))
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Size = new Size(1200, 0), // عرض 1200 – الارتفاع تلقائي
                            Mode = ResizeMode.Max
                        }));

                        await image.SaveAsync(fullPath, new WebpEncoder
                        {
                            Quality = 75 // توازن ممتاز بين الجودة والحجم
                        });
                    }

                    // 🟢 حفظ المسار الجديد
                    sub.ImagePath = "/uploads/subcourses/" + fileName;
                }


                await _context.SaveChangesAsync();

                TempData["Success"] = $"✅ تم حفظ التعديلات بنجاح على الدورة الفرعية: {sub.Title}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "❌ حدث خطأ أثناء تعديل الدورة الفرعية: " + ex.Message;
                ViewBag.Courses = new SelectList(_context.FrontendCourses.Where(c => c.IsActive).OrderBy(c => c.Title), "Id", "Title", model.FrontendCourseId);
                return View(model);
            }
        }

        // 🗑️ حذف
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var sub = await _context.SubCourses.FindAsync(id);
            if (sub != null)
            {
                _context.SubCourses.Remove(sub);
                await _context.SaveChangesAsync();
                TempData["Success"] = "🗑️ تم حذف الدورة الفرعية.";
            }
            return RedirectToAction(nameof(Index));
        }
        // ✅ التحقق من وجود الـ Slug مسبقًا
        [HttpGet]
        public async Task<JsonResult> CheckSlugExists(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return Json(new { exists = false });

            bool exists = await _context.SubCourses.AnyAsync(s => s.Slug == slug);
            return Json(new { exists });
        }

        // 🧠 دالة لتوليد Slug من العنوان العربي
        private string GenerateSlug(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return Guid.NewGuid().ToString("N");

            string str = phrase.Trim().ToLowerInvariant();

            // إزالة التشكيل والمسافات الزائدة
            str = System.Text.RegularExpressions.Regex.Replace(str, @"\s+", "-");
            str = System.Text.RegularExpressions.Regex.Replace(str, @"[^\w\-]", "-");
            str = str.Replace("ـ", "").Replace("_", "-");

            // منع التكرار في الشرطات
            while (str.Contains("--"))
                str = str.Replace("--", "-");

            return str.Trim('-');
        }

    }
}
