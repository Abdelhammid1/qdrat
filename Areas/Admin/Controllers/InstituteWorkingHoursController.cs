using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Helpers;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Instructor;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class InstituteWorkingHoursController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InstituteWorkingHoursController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ Index
        public IActionResult Index()
        {
            var model = _context.InstituteWorkingHours
                .Select(w => new InstituteWorkingHoursViewModel
                {
                    Id = w.Id,
                    DayOfWeek = w.DayOfWeek,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime,
                    BreakStart = w.BreakStart,
                    BreakEnd = w.BreakEnd,
                    IsWorkingDay = w.IsWorkingDay
                }).ToList();

            return View(model); // ✅ تأكد من أن اسم View مطابق لاسم الملف Index.cshtml
        }


        // ✅ Details
        public async Task<IActionResult> Details(int id)
        {
            var workingHour = await _context.InstituteWorkingHours.FindAsync(id);
            if (workingHour == null)
                return NotFound();

            var viewModel = new InstituteWorkingHoursViewModel
            {
                Id = workingHour.Id,
                DayOfWeek = workingHour.DayOfWeek,
                StartTime = workingHour.StartTime,
                EndTime = workingHour.EndTime,
                BreakStart = workingHour.BreakStart,
                BreakEnd = workingHour.BreakEnd
            };

            return View(viewModel);
        }

        // ✅ Create - GET
        public IActionResult Create()
        {
            var model = new InstituteWorkingHoursViewModel
            {
                AvailableDays = DayNameHelper.GetArabicDays()
            };
            return View(model);
        }



        // ✅ Create - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InstituteWorkingHoursViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var entity = new InstituteWorkingHours
            {
                DayOfWeek = model.DayOfWeek,
                StartTime = model.StartTime,
                EndTime = model.EndTime,
                BreakStart = model.BreakStart,
                BreakEnd = model.BreakEnd
            };

            _context.InstituteWorkingHours.Add(entity);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ✅ Edit - GET
        public async Task<IActionResult> Edit(int id)
        {
            var workingHour = await _context.InstituteWorkingHours.FindAsync(id);
            if (workingHour == null)
                return NotFound();

            var model = new InstituteWorkingHoursViewModel
            {
                Id = workingHour.Id,
                DayOfWeek = workingHour.DayOfWeek,
                StartTime = workingHour.StartTime,
                EndTime = workingHour.EndTime,
                BreakStart = workingHour.BreakStart,
                BreakEnd = workingHour.BreakEnd
            };

            return View(model);
        }

        // ✅ Edit - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(InstituteWorkingHoursViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var entity = await _context.InstituteWorkingHours.FindAsync(model.Id);
            if (entity == null)
                return NotFound();

            entity.DayOfWeek = model.DayOfWeek;
            entity.StartTime = model.StartTime;
            entity.EndTime = model.EndTime;
            entity.BreakStart = model.BreakStart;
            entity.BreakEnd = model.BreakEnd;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ✅ Delete - GET
        public async Task<IActionResult> Delete(int id)
        {
            var workingHour = await _context.InstituteWorkingHours.FindAsync(id);
            if (workingHour == null)
                return NotFound();

            var viewModel = new InstituteWorkingHoursViewModel
            {
                Id = workingHour.Id,
                DayOfWeek = workingHour.DayOfWeek,
                StartTime = workingHour.StartTime,
                EndTime = workingHour.EndTime,
                BreakStart = workingHour.BreakStart,
                BreakEnd = workingHour.BreakEnd
            };

            return View(viewModel);
        }

        // ✅ Delete - POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var workingHour = await _context.InstituteWorkingHours.FindAsync(id);
            if (workingHour == null)
                return NotFound();

            _context.InstituteWorkingHours.Remove(workingHour);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
