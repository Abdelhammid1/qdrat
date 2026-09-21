using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class PromoExperiencesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PromoExperiencesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 🔹 عرض جميع العناصر
        public async Task<IActionResult> Index()
        {
            var promos = await _context.PromoExperiences
                .OrderBy(p => p.Order)
                .ToListAsync();
            return View(promos);
        }

        // 🔹 إنشاء جديد
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PromoExperience promo)
        {
            if (!ModelState.IsValid) return View(promo);

            _context.PromoExperiences.Add(promo);
            await _context.SaveChangesAsync();
            TempData["Success"] = "تمت إضافة المحتوى بنجاح ✅";
            return RedirectToAction(nameof(Index));
        }

        // 🔹 تعديل
        public async Task<IActionResult> Edit(int id)
        {
            var promo = await _context.PromoExperiences.FindAsync(id);
            if (promo == null) return NotFound();
            return View(promo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PromoExperience promo)
        {
            if (!ModelState.IsValid) return View(promo);

            _context.PromoExperiences.Update(promo);
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تعديل المحتوى بنجاح ✅";
            return RedirectToAction(nameof(Index));
        }

        // 🔹 حذف
        public async Task<IActionResult> Delete(int id)
        {
            var promo = await _context.PromoExperiences.FindAsync(id);
            if (promo == null) return NotFound();

            _context.PromoExperiences.Remove(promo);
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم حذف العنصر بنجاح 🗑️";
            return RedirectToAction(nameof(Index));
        }
    }
}
