using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Entities;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.ViewModels.Students;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    public class StudySessionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudySessionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            int studentId = 3; // ❗ مؤقتًا حتى يتم ربط الطالب الحالي بالحساب

            var reservations = await _context.StudySessionReservations
                .Include(r => r.Branch)
                .Include(r => r.Instructor)
                .Where(r => r.StudentID == studentId)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            var viewModelList = reservations.Select(r => new StudentSessionReservationListViewModel
            {
                Id = r.Id,

                BranchName = r.Branch?.Name,
                InstructorName = r.Instructor?.FullName ?? "بدون مدرب",
                RequestedDate = r.RequestedDate,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                TotalSeats = r.TotalSeats,
                Fee = r.Fee,
                IsCompleted = r.IsCompleted,
                Status = r.Status
            }).ToList();

            return View(viewModelList);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var session = await _context.StudySessionReservations
                .Include(r => r.Branch)
                .Include(r => r.Instructor)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (session == null)
                return NotFound();

            var viewModel = new StudentSessionReservationListViewModel
            {
                Id = session.Id,
                BranchName = session.Branch?.Name,
                InstructorName = session.Instructor?.FullName ?? "بدون مدرب",
                RequestedDate = session.RequestedDate,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                TotalSeats = session.TotalSeats,
                Fee = session.Fee,
                Status = session.Status
            };

            ViewBag.Services = session.AdditionalServices;
            ViewBag.Room = session.RoomName;
            ViewBag.Notes = session.Notes;

            return View(viewModel);
        }


        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var session = await _context.StudySessionReservations.FindAsync(id);

            if (session == null)
                return NotFound();

            var viewModel = new StudySessionReservationViewModel
            {
                Id = session.Id,
                BranchId = session.BranchId,
                InstructorId = session.InstructorId,
                RequestedDate = session.RequestedDate,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                TotalSeats = session.TotalSeats,
                Fee = session.Fee,
                AdditionalServices = session.AdditionalServices,
                Notes = session.Notes,
                RoomName = session.RoomName,
                BranchList = await GetBranchesSelectList(),
                InstructorList = await GetInstructorsSelectList()
            };

            return View(viewModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StudySessionReservationViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            ModelState.Remove("BranchList");
            ModelState.Remove("InstructorList");

            if (!ModelState.IsValid)
            {
                model.BranchList = await GetBranchesSelectList();
                model.InstructorList = await GetInstructorsSelectList();
                return View(model);
            }

            var session = await _context.StudySessionReservations.FindAsync(id);
            if (session == null)
                return NotFound();

            // التحديث
            session.BranchId = model.BranchId;
            session.InstructorId = model.InstructorId;
            session.RequestedDate = model.RequestedDate;
            session.StartTime = model.StartTime;
            session.EndTime = model.EndTime;
            session.TotalSeats = model.TotalSeats;
            session.Fee = model.Fee;
            session.AdditionalServices = model.AdditionalServices;
            session.Notes = model.Notes;
            session.RoomName = model.RoomName;

            _context.Update(session);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم تعديل الجلسة بنجاح.";
            return RedirectToAction(nameof(Index));
        }



        public async Task<IActionResult> Dashboard()
        {
            int studentId = 3; // لاحقًا يتم استبداله بالمستخدم الحالي

            var now = DateTime.Now;
            var weekStart = now.Date.AddDays(-(int)now.DayOfWeek);
            var weekEnd = weekStart.AddDays(7);

            var reservations = await _context.StudySessionReservations
                .Include(r => r.Branch)
                .Include(r => r.Instructor)
                .Where(r => r.StudentID == studentId)
                .ToListAsync();
            var ratedSessionIds = _context.StudySessionRatings
    .Where(r => r.StudentId == studentId)
    .Select(r => r.SessionId)
    .ToHashSet();
            var nextSession = reservations
     .Where(r => r.RequestedDate >= now.Date && r.Status == StudySessionStatus.Approved)
     .OrderBy(r => r.RequestedDate)
     .FirstOrDefault();

            var viewModel = new StudySessionsDashboardViewModel
            {
                NextSession = nextSession,  // ✅ فقط نعين الجلسة هنا

                ApprovedThisWeekCount = reservations.Count(r =>
                    r.Status == StudySessionStatus.Approved &&
                    r.RequestedDate.Date >= weekStart &&
                    r.RequestedDate.Date <= weekEnd),

                TotalUnpaidFees = reservations
                    .Where(r => r.Fee.HasValue && r.Fee > 0 && r.Status == StudySessionStatus.Approved)
                    .Sum(r => r.Fee.Value),



                AllReservations = reservations.Select(r => new StudentSessionReservationListViewModel
                {
                    Id = r.Id,
                    BranchName = r.Branch?.Name,
                    InstructorName = r.Instructor?.FullName ?? "بدون مدرب",
                    RequestedDate = r.RequestedDate,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    TotalSeats = r.TotalSeats,
                    Fee = r.Fee,
                    Status = r.Status,
                    IsCompleted = r.IsCompleted,
                    IsRated = ratedSessionIds.Contains(r.Id)
                }).ToList()

            };

            return View(viewModel);
        }




        public async Task<IActionResult> Create(int? sectionId = null)
        {
            var viewModel = new StudySessionReservationViewModel
            {
                BranchList = await GetBranchesSelectList(),
                InstructorList = await GetInstructorsSelectList(),
                SuggestedSectionId = sectionId // ✅ تمرير المحور إن وُجد
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StudySessionReservationViewModel model)
        {
            ModelState.Remove("BranchList");
            ModelState.Remove("InstructorList");

            System.Diagnostics.Debug.WriteLine("✅ Create POST triggered");

            if (!ModelState.IsValid)
            {
                model.BranchList = await GetBranchesSelectList();
                model.InstructorList = await GetInstructorsSelectList();
                return View(model);
            }

            var reservation = new StudySessionReservation
            {
                StudentID = 3, // مؤقتًا حتى ربط الحساب
                BranchId = model.BranchId,
                InstructorId = model.InstructorId,
                RequestedDate = model.RequestedDate,
                StartTime = model.StartTime,
                EndTime = model.EndTime,
                AdditionalServices = model.AdditionalServices,
                Fee = model.Fee,
                Status = StudySessionStatus.Pending,
                TotalSeats = model.TotalSeats,
                ReservedSeats = 1,
                CreatedAt = DateTime.UtcNow,
                RoomName = "غير محددة", // ✅ افتراضي
                Notes = "",              // ✅ لتفادي null
                PaymentReference = "N/A" // ✅ لتفادي null
            };

            Console.WriteLine("تم استلام الطلب POST");

            _context.StudySessionReservations.Add(reservation);
            await _context.SaveChangesAsync();
            if (reservation.Id > 0)
            {
                TempData["Success"] = "✅ تم إرسال الطلب بنجاح!";
            }
            else
            {
                TempData["Error"] = "❌ فشل في الإرسال!";
            }

            return RedirectToAction("Index");
        }


        [HttpGet]
        public async Task<IActionResult> Cancel(int id)
        {
            var session = await _context.StudySessionReservations.FindAsync(id);
            if (session == null)
                return NotFound();

            session.Status = StudySessionStatus.Canceled; // أو Rejected حسب ما تفضّل
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إلغاء الجلسة بنجاح.";
            return RedirectToAction("Dashboard"); // أو Index حسب المكان
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var session = await _context.StudySessionReservations.FindAsync(id);
            if (session == null)
                return NotFound();

            _context.StudySessionReservations.Remove(session);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف الجلسة نهائيًا.";
            return RedirectToAction("Dashboard"); // أو Index
        }

        private async Task<List<SelectListItem>> GetBranchesSelectList()
        {
            return await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToListAsync();
        }
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var session = await _context.StudySessionReservations
                .Include(r => r.Branch)
                .Include(r => r.Instructor)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (session == null)
                return NotFound();

            var viewModel = new StudentSessionReservationListViewModel
            {
                Id = session.Id,
                BranchName = session.Branch?.Name,
                InstructorName = session.Instructor?.FullName ?? "بدون مدرب",
                RequestedDate = session.RequestedDate,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                TotalSeats = session.TotalSeats,
                Fee = session.Fee,
                Status = session.Status
            };

            return View(viewModel);
        }
    

        private async Task<List<SelectListItem>> GetInstructorsSelectList()
        {
            return await _context.Instructors
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = i.FullName
                }).ToListAsync();
        }
    }
}
