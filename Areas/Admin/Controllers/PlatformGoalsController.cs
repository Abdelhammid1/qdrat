using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Entities.Frontend;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class PlatformGoalsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public PlatformGoalsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var goals = await _context.PlatformGoals
                .OrderBy(g => g.DisplayOrder)
                .ToListAsync();
            return View(goals);
        }

        [HttpGet]
        public IActionResult Create() => View(new PlatformGoal());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PlatformGoal model)
        {
            if (!ModelState.IsValid)
                return View(model);

            _context.PlatformGoals.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ تم إضافة الهدف بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var goal = await _context.PlatformGoals.FindAsync(id);
            if (goal == null) return NotFound();
            return View(goal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PlatformGoal model)
        {
            if (!ModelState.IsValid)
                return View(model);

            _context.Update(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ تم تعديل الهدف بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var goal = await _context.PlatformGoals.FindAsync(id);
            if (goal != null)
            {
                _context.PlatformGoals.Remove(goal);
                await _context.SaveChangesAsync();
                TempData["Success"] = "🗑️ تم حذف الهدف.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
