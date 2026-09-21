using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    public class InstructorWorkSchedulesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InstructorWorkSchedulesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ Index
        public IActionResult Index(int? instructorId, DayOfWeek? dayOfWeek)
        {
            var query = _context.InstructorWorkSchedules
                .Include(i => i.Instructor)
                .AsQueryable();

            if (instructorId.HasValue)
                query = query.Where(s => s.InstructorId == instructorId);

            if (dayOfWeek.HasValue)
                query = query.Where(s => s.DayOfWeek == dayOfWeek);

            var viewModel = new InstructorScheduleFilterViewModel
            {
                SelectedInstructorId = instructorId,
                SelectedDayOfWeek = dayOfWeek,
                Schedules = query.Select(s => new InstructorWorkScheduleViewModel
                {
                    Id = s.Id,
                    InstructorId = s.InstructorId,
                    InstructorName = s.Instructor.FullName,
                    DayOfWeek = s.DayOfWeek,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    BreakStart = s.BreakStart,
                    BreakEnd = s.BreakEnd,
                    IsWorkingDay = s.IsWorkingDay
                }).ToList(),

                Instructors = GetInstructorSelectList(),
                ArabicDays = GetArabicDaysList()
            };

            return View(viewModel);
        }




        // ✅ Create - GET
        public IActionResult Create()
        {
            var model = new InstructorWorkScheduleViewModel
            {
                Instructors = GetInstructorSelectList(),
                ArabicDays = GetArabicDaysList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InstructorWorkScheduleViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // ✅ إعادة تعبئة القوائم المنسدلة عند فشل التحقق
                model.Instructors = GetInstructorSelectList();
                model.ArabicDays = GetArabicDaysList();
                return View(model);
            }

            // ✅ إنشاء الكيان من الـ ViewModel
            var schedule = new InstructorWorkSchedule
            {
                InstructorId = model.InstructorId,
                DayOfWeek = model.DayOfWeek, // ✔️ لا داعي لتحويل لأن النوع Enum متطابق
                StartTime = model.StartTime,
                EndTime = model.EndTime,
                BreakStart = model.BreakStart,
                BreakEnd = model.BreakEnd,
                IsWorkingDay = model.IsWorkingDay,
                CreatedAt = DateTime.UtcNow
            };

            _context.InstructorWorkSchedules.Add(schedule);
            await _context.SaveChangesAsync();

            // ✅ إعادة التوجيه إلى صفحة القائمة بعد الحفظ
            return RedirectToAction(nameof(Index));
        }


        // ✅ Edit - GET
        public async Task<IActionResult> Edit(int id)
        {
            var work = await _context.InstructorWorkSchedules.FindAsync(id);
            if (work == null) return NotFound();

            var model = new InstructorWorkScheduleViewModel
            {
                Id = work.Id,
                InstructorId = work.InstructorId,
                DayOfWeek = work.DayOfWeek,
                StartTime = work.StartTime,
                EndTime = work.EndTime,
                BreakStart = work.BreakStart,
                BreakEnd = work.BreakEnd,
                IsWorkingDay = work.IsWorkingDay,
                Instructors = GetInstructorsList(),
                ArabicDays = GetArabicDaysList()
            };

            return View(model);
        }

        // ✅ Edit - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(InstructorWorkScheduleViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Instructors = GetInstructorsList();
                model.ArabicDays = GetArabicDaysList();
                return View(model);
            }

            var entity = await _context.InstructorWorkSchedules.FindAsync(model.Id);
            if (entity == null) return NotFound();

            entity.InstructorId = model.InstructorId;
            entity.DayOfWeek = model.DayOfWeek;
            entity.StartTime = model.StartTime;
            entity.EndTime = model.EndTime;
            entity.BreakStart = model.BreakStart;
            entity.BreakEnd = model.BreakEnd;
            entity.IsWorkingDay = model.IsWorkingDay;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ✅ Details
        public async Task<IActionResult> Details(int id)
        {
            var entity = await _context.InstructorWorkSchedules
                .Include(w => w.Instructor)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (entity == null) return NotFound();

            var model = new InstructorWorkScheduleViewModel
            {
                Id = entity.Id,
                InstructorId = entity.InstructorId,
                InstructorName = entity.Instructor.FullName,
                DayOfWeek = entity.DayOfWeek,
                StartTime = entity.StartTime,
                EndTime = entity.EndTime,
                BreakStart = entity.BreakStart,
                BreakEnd = entity.BreakEnd,
                IsWorkingDay = entity.IsWorkingDay
            };

            return View(model);
        }

        // ✅ Delete - GET
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.InstructorWorkSchedules
                .Include(w => w.Instructor)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (entity == null) return NotFound();

            var model = new InstructorWorkScheduleViewModel
            {
                Id = entity.Id,
                InstructorId = entity.InstructorId,
                InstructorName = entity.Instructor.FullName,
                DayOfWeek = entity.DayOfWeek,
                StartTime = entity.StartTime,
                EndTime = entity.EndTime,
                BreakStart = entity.BreakStart,
                BreakEnd = entity.BreakEnd,
                IsWorkingDay = entity.IsWorkingDay
            };

            return View(model);
        }

        // ✅ Delete - POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var entity = await _context.InstructorWorkSchedules.FindAsync(id);
            if (entity == null) return NotFound();

            _context.InstructorWorkSchedules.Remove(entity);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // 🔹 قوائم المساعدة
        private List<SelectListItem> GetInstructorsList()
        {
            return _context.Instructors
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = i.FullName
                }).ToList();
        }
        // 🔹 قائمة المدربين للقائمة المنسدلة
        private List<SelectListItem> GetInstructorSelectList()
        {
            return _context.Instructors
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = i.FullName
                }).ToList();
        }


        // 🔹 قائمة الأيام بالعربي للقائمة المنسدلة


        private List<SelectListItem> GetArabicDaysList()
        {
            return Enum.GetValues(typeof(DayOfWeek))
                .Cast<DayOfWeek>()
                .Select(d => new SelectListItem
                {
                    Value = ((int)d).ToString(),
                    Text = DayNameHelper.GetArabicDay(d)
                })
                .ToList();
        }


    }
}
