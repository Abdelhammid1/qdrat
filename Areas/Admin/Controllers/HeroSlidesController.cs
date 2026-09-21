using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]

    public class HeroSlidesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public HeroSlidesController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var slides = await _context.HeroSlides.OrderBy(s => s.DisplayOrder).ToListAsync();
            return View(slides);
        }

        public IActionResult Create()
        {
            // ✅ تمرير كائن فارغ من النوع المطلوب لتفادي NullReference
            return View(new HeroSlide());
        }




[HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HeroSlide model, IFormFile imageFile)
    {
        try
        {
            // ✅ تحقق من المدخلات
            if (string.IsNullOrWhiteSpace(model.Title))
            {
                ViewData["ErrorMessage"] = "⚠️ يجب إدخال عنوان الشريحة.";
                return View(model);
            }

            if (imageFile == null || imageFile.Length == 0)
            {
                ViewData["ErrorMessage"] = "⚠️ يجب رفع صورة الخلفية للشريحة.";
                return View(model);
            }

            // ✅ مسار الحفظ
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "slides");
            Directory.CreateDirectory(uploadsDir);

            // ✅ اسم ملف WebP
            var fileName = $"{Guid.NewGuid()}.webp";
            var filePath = Path.Combine(uploadsDir, fileName);

            // ✅ تحميل + تحويل + ضغط الصورة
            using (var image = await Image.LoadAsync(imageFile.OpenReadStream()))
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(1920, 0), // عرض مناسب للسلايدر
                    Mode = ResizeMode.Max
                }));

                await image.SaveAsync(filePath, new WebpEncoder
                {
                    Quality = 75 // جودة ممتازة وحجم خفيف
                });
            }

            // ✅ حفظ البيانات
            model.ImagePath = "/uploads/slides/" + fileName;
            model.CreatedAt = DateTime.Now;

            _context.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ تم إنشاء الشريحة بنجاح.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ViewData["ErrorMessage"] = "❌ حدث خطأ أثناء إنشاء الشريحة: " + ex.Message;
            return View(model);
        }
    }


    public async Task<IActionResult> Edit(int id)
        {
            var slide = await _context.HeroSlides.FindAsync(id);
            if (slide == null) return NotFound();
            return View(slide);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, HeroSlide model, IFormFile? imageFile)
        {
            try
            {
                var slide = await _context.HeroSlides.FindAsync(id);
                if (slide == null)
                    return NotFound();

                // 🔹 تحديث البيانات النصية
                slide.Title = model.Title;
                slide.Description = model.Description;
                slide.ButtonText = model.ButtonText;
                slide.ButtonUrl = model.ButtonUrl;
                slide.ShowButton = model.ShowButton;
                slide.Alignment = model.Alignment;
                slide.DisplayOrder = model.DisplayOrder;
                slide.IsActive = model.IsActive;

                // 🔹 في حال رفع صورة جديدة
                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "slides");
                    Directory.CreateDirectory(uploadsDir);

                    var fileName = $"{Guid.NewGuid()}.webp";
                    var filePath = Path.Combine(uploadsDir, fileName);

                    // ✅ تحميل + تحويل + ضغط
                    using (var image = await Image.LoadAsync(imageFile.OpenReadStream()))
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Size = new Size(1920, 0),
                            Mode = ResizeMode.Max
                        }));

                        await image.SaveAsync(filePath, new WebpEncoder
                        {
                            Quality = 75
                        });
                    }

                    // (اختياري) حذف الصورة القديمة من السيرفر
                    if (!string.IsNullOrWhiteSpace(slide.ImagePath))
                    {
                        var oldPath = Path.Combine(
                            _env.WebRootPath,
                            slide.ImagePath.TrimStart('/')
                        );

                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
                    }

                    slide.ImagePath = "/uploads/slides/" + fileName;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "✅ تم تحديث الشريحة بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewData["ErrorMessage"] = "❌ حدث خطأ أثناء تعديل الشريحة: " + ex.Message;
                return View(model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var slide = await _context.HeroSlides.FindAsync(id);
            if (slide != null)
            {
                _context.HeroSlides.Remove(slide);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
