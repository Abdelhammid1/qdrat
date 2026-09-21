using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class SocialMediaLinksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SocialMediaLinksController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var links = await _context.SocialMediaLinks
                .OrderBy(l => l.DisplayOrder)
                .ToListAsync();

            return View(links);
        }

        public IActionResult Create()
        {
            return View(new SocialMediaLink());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SocialMediaLink model)
        {
            if (!ModelState.IsValid) return View(model);

            _context.SocialMediaLinks.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم إضافة الرابط بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var link = await _context.SocialMediaLinks.FindAsync(id);
            if (link == null) return NotFound();

            return View(link);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SocialMediaLink model)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View(model);

            _context.Update(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم تحديث الرابط بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var link = await _context.SocialMediaLinks.FindAsync(id);
            if (link == null) return NotFound();

            _context.SocialMediaLinks.Remove(link);
            await _context.SaveChangesAsync();

            TempData["Success"] = "🗑️ تم حذف الرابط.";
            return RedirectToAction(nameof(Index));
        }
    }
}
