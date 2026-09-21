using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Security;
using QdratNew.Services.AI;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Section;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class SectionsController : Controller
    {
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _context;

        public SectionsController(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }
        public async Task<IActionResult> AIAnalysis(int id)
        {
            var analyzer = new SectionAIAnalyzer(_context);
            var (score, notes) = await analyzer.AnalyzeSectionAsync(id);

            var section = await _context.Sections.FindAsync(id);
            if (section != null)
            {
                section.AIScore = score;
                section.AINotes = notes;
                _context.Sections.Update(section);
                await _context.SaveChangesAsync();
            }

            TempData["AIScore"] = $"📊 النتيجة: {score * 100}%";
            TempData["AINotes"] = notes;

            return RedirectToAction("Details", new { id });
        }

        // 🔹 GET: Index
        public async Task<IActionResult> Index(int? curriculumId)
        {
            var curriculums = await _context.Curriculums
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title
                }).ToListAsync();

            ViewData["Curriculums"] = curriculums;

            var sectionsQuery = _context.Sections
                .Include(s => s.Curriculum)
                .AsQueryable();

            if (curriculumId.HasValue)
            {
                sectionsQuery = sectionsQuery.Where(s => s.CurriculumId == curriculumId.Value);
            }

            var sections = await sectionsQuery.ToListAsync();
            return View(sections);
        }



        // 🔹 GET: Create
        public async Task<IActionResult> Create()
        {
            var viewModel = new SectionViewModel
            {
                Curriculums = await _context.Curriculums
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title }).ToListAsync()
            };
            return View(viewModel);
        }

        // 🔹 POST: Create
        [HttpPost]
        public async Task<IActionResult> Create(SectionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // ⬅️ إعادة تعبئة القائمة المنسدلة عند فشل ModelState
                model.Curriculums = await _context.Curriculums
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.Title
                    }).ToListAsync();

                TempData["Error"] = "⚠️ حدث خطأ في إدخال البيانات. يرجى التأكد من جميع الحقول.";

                return View(model);
            }

            var section = new Section
            {
                Title = model.Title,
                CurriculumId = model.CurriculumId
            };

            _context.Sections.Add(section);
            await _context.SaveChangesAsync();
            _cache.Remove("SectionsCache");

            TempData["Success"] = "✅ تم إضافة المحور بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // 🔹 GET: Edit
        public async Task<IActionResult> Edit(int id)
        {
            var section = await _context.Sections.FindAsync(id);
            if (section == null) return NotFound();

            var model = new SectionViewModel
            {
                Id = section.Id,
                Title = section.Title,
                CurriculumId = section.CurriculumId,
                Curriculums = await _context.Curriculums
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title }).ToListAsync()
            };

            return View(model);
        }


        [HttpGet]
        [AdminPermission("Sections", "FilterList")]
        public async Task<IActionResult> GetForFilter(int? curriculumId)
        {
            var query = _context.Sections
                .AsNoTracking()
                .AsQueryable();

            if (curriculumId.HasValue && curriculumId.Value > 0)
            {
                query = query.Where(x => x.CurriculumId == curriculumId.Value);
            }

            var data = await query
                .OrderBy(x => x.Title)
                .Select(x => new
                {
                    id = x.Id,
                    title = x.Title
                })
                .ToListAsync();

            return Json(data);
        }






        // 🔹 POST: Edit
        [HttpPost]
        public async Task<IActionResult> Edit(int id, SectionViewModel model)
        {
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                model.Curriculums = await _context.Curriculums
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title }).ToListAsync();
                return View(model);
            }

            var section = await _context.Sections.FindAsync(id);
            if (section == null) return NotFound();

            section.Title = model.Title;
            section.CurriculumId = model.CurriculumId;

            _context.Sections.Update(section);
            await _context.SaveChangesAsync();
            _cache.Remove("SectionsCache");

            return RedirectToAction(nameof(Index));
        }

        // 🔹 GET: Details
        public async Task<IActionResult> Details(int id)
        {
            var section = await _context.Sections
                .Include(s => s.Curriculum)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null) return NotFound();
            return View(section);
        }

        // 🔹 GET: Delete
        public async Task<IActionResult> Delete(int id)
        {
            var section = await _context.Sections
                .Include(s => s.Curriculum)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null) return NotFound();
            return View(section);
        }

        // 🔹 POST: Delete
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var section = await _context.Sections.FindAsync(id);
            if (section == null) return NotFound();

            _context.Sections.Remove(section);
            await _context.SaveChangesAsync();
            _cache.Remove("SectionsCache");

            return RedirectToAction(nameof(Index));
        }
    }
}
