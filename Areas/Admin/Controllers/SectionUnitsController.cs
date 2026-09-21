using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Section;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class SectionUnitsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SectionUnitsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/SectionUnits/Create
        public async Task<IActionResult> Create()
        {
            var model = new SectionUnitAssignmentViewModel
            {
                Sections = await _context.Sections
                    .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title })
                    .ToListAsync(),

                Units = await _context.Units
                    .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.Title })
                    .ToListAsync()
            };

            return View(model);
        }

        // POST: Admin/SectionUnits/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SectionUnitAssignmentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                Console.WriteLine("❗ ModelState غير صالح. الأخطاء:");
                foreach (var error in ModelState)
                {
                    foreach (var subError in error.Value.Errors)
                    {
                        Console.WriteLine($"- الحقل: {error.Key}، الخطأ: {subError.ErrorMessage}");
                    }
                }

                model.Sections = await _context.Sections
                    .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title })
                    .ToListAsync();

                model.Units = await _context.Units
                    .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.Title })
                    .ToListAsync();

                return View(model);
            }

            Console.WriteLine($"✅ تم اجتياز ModelState: SectionId={model.SectionId}, SelectedUnits={model.SelectedUnitIds.Count}");

            if (model.SelectedUnitIds == null || !model.SelectedUnitIds.Any())
            {
                Console.WriteLine("⚠️ لم يتم اختيار أي وحدة.");
            }

            // حذف أي ربط قديم لهذا المحور
            var existing = _context.SectionUnits.Where(su => su.SectionId == model.SectionId);
            _context.SectionUnits.RemoveRange(existing);

            // إنشاء الربط الجديد
            foreach (var unitId in model.SelectedUnitIds)
            {
                Console.WriteLine($"➕ إضافة ربط: SectionId={model.SectionId}, UnitId={unitId}");

                var su = new SectionUnit
                {
                    SectionId = model.SectionId,
                    UnitId = unitId
                };
                _context.SectionUnits.Add(su);
            }

            var result = await _context.SaveChangesAsync();
            Console.WriteLine($"💾 عدد السجلات التي تم حفظها: {result}");

            TempData["SuccessMessage"] = "✅ تم ربط الوحدات بالمحور بنجاح!";
            return RedirectToAction(nameof(Create));
        }
        // ✅ Index - عرض المحاور مع الوحدات
        public async Task<IActionResult> Index()
        {
            var data = await _context.Sections
                .Include(s => s.SectionUnits)
                    .ThenInclude(su => su.Unit)
                .Select(s => new SectionUnitsViewModel
                {
                    SectionId = s.Id,
                    SectionTitle = s.Title,
                    UnitTitles = s.SectionUnits.Select(su => su.Unit.Title).ToList()
                })
                .ToListAsync();

            return View(data);
        }

        // ✅ GET: Edit - عرض صفحة التعديل
        public async Task<IActionResult> Edit(int id)
        {
            var section = await _context.Sections
                .Include(s => s.SectionUnits)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null)
                return NotFound();

            var model = new SectionUnitAssignmentViewModel
            {
                SectionId = section.Id,
                SelectedUnitIds = section.SectionUnits.Select(su => su.UnitId).ToList(),

                Sections = await _context.Sections
                    .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title })
                    .ToListAsync(),

                Units = await _context.Units
                    .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.Title })
                    .ToListAsync()
            };

            return View(model);
        }

        // ✅ POST: Edit - حفظ التعديلات
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SectionUnitAssignmentViewModel model)
        {
            if (id != model.SectionId)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                model.Sections = await _context.Sections
                    .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title })
                    .ToListAsync();

                model.Units = await _context.Units
                    .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.Title })
                    .ToListAsync();

                return View(model);
            }

            // حذف القديم
            var existing = _context.SectionUnits.Where(su => su.SectionId == model.SectionId);
            _context.SectionUnits.RemoveRange(existing);

            // إضافة الجديد
            foreach (var unitId in model.SelectedUnitIds)
            {
                var su = new SectionUnit
                {
                    SectionId = model.SectionId,
                    UnitId = unitId
                };
                _context.SectionUnits.Add(su);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ تم تعديل ربط الوحدات بالمحور بنجاح!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
     
        public async Task<IActionResult> Manage(int unitId)
        {
            var unit = await _context.Units
                .Include(u => u.SectionUnits)
                .FirstOrDefaultAsync(u => u.Id == unitId);

            if (unit == null)
                return NotFound();

            // أولاً نجلب كل المحاور بدون عمليات Linq معقدة
            var allSections = await _context.Sections
                .Select(s => new { s.Id, s.Title })
                .ToListAsync();

            // ثم نجهز الموديل يدويًا
            var model = new ManageSectionUnitViewModel
            {
                UnitId = unitId,
                UnitTitle = unit.Title,
                Sections = allSections.Select(s => new SectionCheckboxViewModel
                {
                    SectionId = s.Id,
                    Title = s.Title,
                    IsLinked = unit.SectionUnits.Any(su => su.SectionId == s.Id) // ✅ يتم المعالجة هنا بعد جلب الداتا
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(int unitId, List<int> SelectedSectionIds)
        {
            var unit = await _context.Units
                .Include(u => u.SectionUnits)
                .FirstOrDefaultAsync(u => u.Id == unitId);

            if (unit == null)
            {
                return NotFound();
            }

            // حذف كل الربوط القديمة
            _context.SectionUnits.RemoveRange(unit.SectionUnits);

            // إضافة الربوط الجديدة
            if (SelectedSectionIds != null && SelectedSectionIds.Any())
            {
                foreach (var sectionId in SelectedSectionIds)
                {
                    unit.SectionUnits.Add(new SectionUnit
                    {
                        UnitId = unitId,
                        SectionId = sectionId
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ تم تحديث الربط بين الوحدة والمحاور بنجاح.";

            return RedirectToAction("Index", "Units");
        }


        // ✅ Details - عرض تفاصيل محور مع وحداته
        public async Task<IActionResult> Details(int id)
        {
            var section = await _context.Sections
                .Include(s => s.SectionUnits)
                    .ThenInclude(su => su.Unit)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null)
                return NotFound();

            var model = new SectionUnitsViewModel
            {
                SectionId = section.Id,
                SectionTitle = section.Title,
                UnitTitles = section.SectionUnits.Select(su => su.Unit.Title).ToList()
            };

            return View(model);
        }

        // ✅ Delete - حذف كل الوحدات المرتبطة بمحور
        public async Task<IActionResult> Delete(int id)
        {
            var section = await _context.Sections
                .Include(s => s.SectionUnits)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null)
                return NotFound();

            var model = new SectionUnitsViewModel
            {
                SectionId = section.Id,
                SectionTitle = section.Title,
                UnitTitles = section.SectionUnits.Select(su => su.Unit.Title).ToList()
            };

            return View(model);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var sectionUnits = _context.SectionUnits.Where(su => su.SectionId == id);
            _context.SectionUnits.RemoveRange(sectionUnits);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "🗑️ تم حذف جميع الوحدات المرتبطة بالمحور.";
            return RedirectToAction(nameof(Index));
        }
    }
}

