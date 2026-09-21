using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class CorporateRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CorporateRequestsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var data = await _context.CorporateRegistrationRequests
                .OrderByDescending(x => x.SubmittedAt)
                .ToListAsync();
            return View(data);
        }


        [HttpGet]
        public async Task<IActionResult> EditStatus(int id)
        {
            var req = await _context.CorporateRegistrationRequests.FindAsync(id);
            if (req == null) return NotFound();

            var vm = new EditCorporateStatusViewModel
            {
                Id = req.Id,
                FullName = req.FullName,
                CurrentStatus = req.Status,
                AdminNotes = req.AdminNotes
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStatus(EditCorporateStatusViewModel vm)
        {
            var req = await _context.CorporateRegistrationRequests.FindAsync(vm.Id);
            if (req == null) return NotFound();

            req.Status = vm.CurrentStatus;
            req.AdminNotes = vm.AdminNotes;
            req.LastUpdated = DateTime.Now;
            req.HandledBy = User.Identity.Name;

            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ تم تحديث حالة الطلب بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> SetAutoCloseDate(string company, DateTime autoCloseAt)
        {
            var setting = await _context.CorporateRegistrationSettings
                .FirstOrDefaultAsync(x => x.CompanyName == company);

            if (setting != null)
            {
                setting.AutoCloseAt = autoCloseAt;
                setting.IsClosed = false; // يبقى مفتوحًا حتى يصل الوقت المحدد
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "تم تعيين موعد الإغلاق التلقائي بنجاح.";
            return RedirectToAction("ManageCompany", new { company });
        }


        [HttpPost]
        public async Task<IActionResult> ClearAutoCloseDate(string company)
        {
            if (string.IsNullOrWhiteSpace(company))
                return BadRequest("Company name is required.");

            var setting = await _context.CorporateRegistrationSettings
                .FirstOrDefaultAsync(x => x.CompanyName == company);

            if (setting == null)
                return NotFound("Company setting not found.");

            setting.AutoCloseAt = null;  // إزالة الموعد
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حذف موعد الإغلاق التلقائي.";

            return RedirectToAction("ManageCompany", new { company });
        }


        [HttpGet]
        public async Task<IActionResult> ManageCompany(string company)
        {
            var setting = await _context.CorporateRegistrationSettings
                .FirstOrDefaultAsync(x => x.CompanyName == company);

            if (setting == null)
            {
                setting = new CorporateRegistrationSetting
                {
                    CompanyName = company,
                    IsClosed = false
                };

                _context.CorporateRegistrationSettings.Add(setting);
                await _context.SaveChangesAsync();
            }

            return View(setting);
        }

        [HttpPost]
        public async Task<IActionResult> CloseRegistration(string company)
        {
            var setting = await _context.CorporateRegistrationSettings
                .FirstOrDefaultAsync(x => x.CompanyName == company);

            if (setting != null)
            {
                setting.IsClosed = true;
                setting.ClosedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "تم إغلاق التسجيل بنجاح.";
            return RedirectToAction("ManageCompany", new { company });
        }


        [HttpPost]
        public async Task<IActionResult> OpenRegistration(string company)
        {
            var setting = await _context.CorporateRegistrationSettings
                .FirstOrDefaultAsync(x => x.CompanyName == company);

            if (setting != null)
            {
                setting.IsClosed = false;
                setting.ReopenedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "تم إعادة فتح التسجيل.";
            return RedirectToAction("ManageCompany", new { company });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleHomeVisibility(string company)
        {
            if (string.IsNullOrWhiteSpace(company))
                return BadRequest("Company name is required.");

            var setting = await _context.CorporateRegistrationSettings
                .FirstOrDefaultAsync(x => x.CompanyName == company);

            if (setting == null)
                return NotFound("Company setting not found.");

            setting.IsVisibleOnHomePage = !setting.IsVisibleOnHomePage;
            await _context.SaveChangesAsync();

            TempData["Success"] = setting.IsVisibleOnHomePage
                ? "تم إظهار قسم الشركة في الصفحة الرئيسية."
                : "تم إخفاء قسم الشركة من الصفحة الرئيسية.";

            return RedirectToAction("ManageCompany", new { company });
        }

        public IActionResult ExportToExcel()
        {
            var data = _context.CorporateRegistrationRequests.ToList();

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("طلبات التسجيل");
            ws.Cell(1, 1).InsertTable(data);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "طلبات_التسجيل.xlsx");
        }
    }

}
