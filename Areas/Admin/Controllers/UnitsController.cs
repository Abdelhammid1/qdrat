using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class UnitsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UnitsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var units = await _context.Units
                .Include(u => u.SectionUnits)
                    .ThenInclude(su => su.Section)
                .ToListAsync();

            var model = new UnitIndexViewModel
            {
                Units = units.Select(u => new UnitWithSectionsViewModel
                {
                    Id = u.Id,
                    Title = u.Title,
                    SectionTitles = u.SectionUnits.Select(su => su.Section.Title).ToList()
                }).ToList()
            };

            return View(model);
        }


        // ✅ Create - GET
        public IActionResult Create()
        {
            var viewModel = new UnitViewModel();
            return View(viewModel);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UnitViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var unit = new Unit
            {
                Title = model.Title,
                // بدون SectionId هنا ✅
            };

            _context.Units.Add(unit);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم إضافة الوحدة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Units/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var unit = await _context.Units.FindAsync(id);
            if (unit == null)
                return NotFound();

            var viewModel = new UnitViewModel
            {
                Id = unit.Id,
                Title = unit.Title
                // لا حاجة للـ SectionId أو قوائم
            };

            return View(viewModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UnitViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var unit = await _context.Units.FindAsync(model.Id);
            if (unit == null)
                return NotFound();

            unit.Title = model.Title;
            // لا تغيير في SectionId

            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ تم تعديل الوحدة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Units/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var unit = await _context.Units.FindAsync(id);

            if (unit == null)
                return NotFound();

            var viewModel = new UnitViewModel
            {
                Id = unit.Id,
                Title = unit.Title
                // ❌ لا نحمل Section أو SectionList
            };

            return View(viewModel);
        }

        // GET: Admin/Units/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var unit = await _context.Units.FindAsync(id);

            if (unit == null)
                return NotFound();

            var viewModel = new UnitViewModel
            {
                Id = unit.Id,
                Title = unit.Title
                // ❌ لا نحمل Section أو SectionList
            };

            return View(viewModel);
        }


        // POST: Admin/Units/DeleteConfirmed/5
        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var unit = await _context.Units.FindAsync(id);
            if (unit == null)
                return NotFound();

            _context.Units.Remove(unit);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        private async Task<List<SelectListItem>> GetSectionsList()
        {
            return await _context.Sections
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Title
                }).ToListAsync();
        }



    }
}
