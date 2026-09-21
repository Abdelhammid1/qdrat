using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Security;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Lesson;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class LessonsController : Controller
    {
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _context;

        public LessonsController(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }
     
        public async Task<IActionResult> Index(int? selectedCurriculumId, int? selectedSectionId, int? selectedUnitId)
        {
            var query = _context.Lessons
                .Include(l => l.Unit)
                .Include(l => l.Section)
                    .ThenInclude(s => s.Curriculum)
                .AsQueryable();

            if (selectedCurriculumId.HasValue)
                query = query.Where(l => l.Section.CurriculumId == selectedCurriculumId.Value);

            if (selectedSectionId.HasValue)
                query = query.Where(l => l.SectionId == selectedSectionId.Value);

            if (selectedUnitId.HasValue)
                query = query.Where(l => l.UnitId == selectedUnitId.Value);

            var lessons = await query
                .Select(l => new LessonViewModel
                {
                    Id = l.Id,
                    Title = l.Title,
                    Content = l.Content,
                    UnitTitle = l.Unit.Title,
                    SectionTitle = l.Section.Title,
                    IsQuantitative = l.Section.Curriculum.IsQuantitative
                }).ToListAsync();

            var vm = new LessonIndexViewModel
            {
                SelectedCurriculumId = selectedCurriculumId,
                SelectedSectionId = selectedSectionId,
                SelectedUnitId = selectedUnitId,
                Lessons = lessons,

                CurriculumList = await _context.Curriculums
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                    .ToListAsync(),

                SectionList = await _context.Sections
                    .Where(s => !selectedCurriculumId.HasValue || s.CurriculumId == selectedCurriculumId.Value)
                    .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title })
                    .ToListAsync(),

                UnitList = await _context.SectionUnits
                    .Where(su => !selectedSectionId.HasValue || su.SectionId == selectedSectionId.Value)
                    .Select(su => new SelectListItem { Value = su.Unit.Id.ToString(), Text = su.Unit.Title })
                    .Distinct()
                    .ToListAsync()
            };

            return View(vm);
        }




        [HttpGet]
        [AdminPermission("Lessons", "FilterList")]
        public async Task<IActionResult> GetBySection(int id)
        {
            var data = await _context.Lessons
                .AsNoTracking()
                .Where(x => x.SectionId == id)
                .OrderBy(x => x.Title)
                .Select(x => new
                {
                    id = x.Id,
                    title = x.Title
                })
                .ToListAsync();

            return Json(data);
        }

        // ✅ Create - GET
        public async Task<IActionResult> Create()
        {
            var model = new LessonCreateViewModel
            {
                Sections = await _context.Sections
                    .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title })
                    .ToListAsync(),
                Units = new List<SelectListItem>()
            };

            return View(model);
        }

        // ✅ Create - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LessonCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var lesson = new Lesson
                {
                    Title = model.Title,
                    Content = model.Content,
                    UnitId = model.SelectedUnitId,
                    SectionId = model.SelectedSectionId,  // إضافة الربط المباشر
                     IsActive = model.IsActive // ✅ الإضافة هنا

                };


                _context.Lessons.Add(lesson);

                var exists = await _context.SectionUnits
                    .AnyAsync(su => su.SectionId == model.SelectedSectionId && su.UnitId == model.SelectedUnitId);

                if (!exists)
                {
                    _context.SectionUnits.Add(new SectionUnit
                    {
                        SectionId = model.SelectedSectionId,
                        UnitId = model.SelectedUnitId
                    });
                }

                await _context.SaveChangesAsync();
                _cache.Remove("SectionsCache");

                TempData["SuccessMessage"] = "تم إنشاء المؤشر بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            model.Sections = await _context.Sections
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title })
                .ToListAsync();

            model.Units = model.SelectedSectionId > 0
                ? await _context.SectionUnits
                    .Where(su => su.SectionId == model.SelectedSectionId)
                    .Select(su => new SelectListItem { Value = su.Unit.Id.ToString(), Text = su.Unit.Title })
                    .ToListAsync()
                : new List<SelectListItem>();

            return View(model);
        }

        // ✅ GetUnitsBySectionId
        [HttpGet]
        public async Task<IActionResult> GetUnitsBySectionId(int sectionId)
        {
            var units = await _context.SectionUnits
                .Where(su => su.SectionId == sectionId)
                .Include(su => su.Unit)
                .Select(su => new { value = su.Unit.Id, text = su.Unit.Title })
                .Distinct()
                .ToListAsync();

            return Json(units);
        }

        // ✅ Edit - GET
        // ✅ Edit - GET
        public async Task<IActionResult> Edit(int id)
        {
            var lesson = await _context.Lessons
                .Include(l => l.Unit)
                .Include(l => l.Section)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null)
                return NotFound();

            int selectedSectionId = lesson.SectionId;
            int selectedUnitId = lesson.UnitId;

            var units = await _context.SectionUnits
                .Where(su => su.SectionId == selectedSectionId)
                .Select(su => new SelectListItem
                {
                    Value = su.Unit.Id.ToString(),
                    Text = su.Unit.Title,
                    Selected = su.UnitId == selectedUnitId
                })
                .ToListAsync();

            var sections = await _context.Sections
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Title,
                    Selected = s.Id == selectedSectionId
                })
                .ToListAsync();

            sections.Insert(0, new SelectListItem { Value = "", Text = "-- اختر المحور --" });
            units.Insert(0, new SelectListItem { Value = "", Text = "-- اختر الوحدة --" });

            var model = new LessonCreateViewModel
            {
                Title = lesson.Title,
                Content = lesson.Content,
                SelectedUnitId = selectedUnitId,
                SelectedSectionId = selectedSectionId,
                Sections = sections,
                Units = units,
                IsActive = lesson.IsActive // ✅ هنا الإضافة المهمة
            };


            return View(model);
        }


        // ✅ Edit - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LessonCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var lesson = await _context.Lessons.FindAsync(id);
                if (lesson == null)
                    return NotFound();

                lesson.Title = model.Title;
                lesson.Content = model.Content;
                lesson.UnitId = model.SelectedUnitId;
                lesson.SectionId = model.SelectedSectionId; // ✅ هذا هو الحل الحقيقي
                lesson.IsActive = model.IsActive; // ✅ لازم تحفظ التغيير


                var exists = await _context.SectionUnits
                    .AnyAsync(su => su.SectionId == model.SelectedSectionId && su.UnitId == model.SelectedUnitId);

                if (!exists)
                {
                    _context.SectionUnits.Add(new SectionUnit
                    {
                        SectionId = model.SelectedSectionId,
                        UnitId = model.SelectedUnitId
                    });
                }

                _context.Update(lesson);
                await _context.SaveChangesAsync();
                _cache.Remove("SectionsCache");

                TempData["SuccessMessage"] = "تم تعديل المؤشر بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            model.Sections = await _context.Sections
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title })
                .ToListAsync();

            model.Units = model.SelectedSectionId > 0
                ? await _context.SectionUnits
                    .Where(su => su.SectionId == model.SelectedSectionId)
                    .Select(su => new SelectListItem { Value = su.Unit.Id.ToString(), Text = su.Unit.Title })
                    .ToListAsync()
                : new List<SelectListItem>();

            return View(model);
        }

        // ✅ Details
        public async Task<IActionResult> Details(int id)
        {
            var lesson = await _context.Lessons
                .Include(l => l.Unit)
                    .ThenInclude(u => u.SectionUnits)
                        .ThenInclude(su => su.Section)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null)
                return NotFound();

            return View(lesson);
        }

        // ✅ Delete - GET
        public async Task<IActionResult> Delete(int id)
        {
            var lesson = await _context.Lessons
                .Include(l => l.Unit)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null)
                return NotFound();

            return View(lesson);
        }

        // ✅ Delete - POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var lesson = await _context.Lessons.FindAsync(id);
            if (lesson == null)
                return NotFound();

            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();
            _cache.Remove("SectionsCache");

            TempData["SuccessMessage"] = "تم حذف المؤشر بنجاح.";
            return RedirectToAction(nameof(Index));
        }
    }
}
