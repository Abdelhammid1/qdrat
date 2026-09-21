using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class StaticPagesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StaticPagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ عرض جميع الصفحات
        public async Task<IActionResult> Index()
        {
            var pages = await _context.StaticPages
                .OrderBy(p => p.Title)
                .ToListAsync();
            return View(pages);
        }

        // ✅ تعديل صفحة
        public async Task<IActionResult> Edit(int id)
        {
            var page = await _context.StaticPages.FindAsync(id);
            if (page == null) return NotFound();
            return View(page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(StaticPage model)
        {
            if (!ModelState.IsValid) return View(model);

            model.UpdatedAt = DateTime.Now;
            _context.StaticPages.Update(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم تحديث الصفحة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // ✅ إنشاء صفحة جديدة
        public IActionResult Create()
        {
            return View(new StaticPage()); // ✅ تمرير كائن جديد يمنع NullReference
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StaticPage model)
        {
            if (!ModelState.IsValid) return View(model);

            _context.StaticPages.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تمت إضافة الصفحة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // ✅ حذف
        public async Task<IActionResult> Delete(int id)
        {
            var page = await _context.StaticPages.FindAsync(id);
            if (page == null) return NotFound();

            _context.StaticPages.Remove(page);
            await _context.SaveChangesAsync();

            TempData["Success"] = "🗑️ تم حذف الصفحة بنجاح";
            return RedirectToAction(nameof(Index));
        }
    }
}
