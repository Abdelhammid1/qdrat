using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Section;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class StudySessionApprovalsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudySessionApprovalsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string statusFilter, DateTime? fromDate, DateTime? toDate, string completionFilter)
        {
            var query = _context.StudySessionReservations
                .Include(r => r.Student)
                .Include(r => r.Branch)
                .Include(r => r.Instructor)
                .AsQueryable();

            // ✅ فلترة بالحالة
            if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<StudySessionStatus>(statusFilter, out var parsedStatus))
                query = query.Where(r => r.Status == parsedStatus);

            // ✅ فلترة بإتمام الجلسة
            if (!string.IsNullOrEmpty(completionFilter))
            {
                if (completionFilter == "completed")
                    query = query.Where(r => r.IsCompleted == true);
                else if (completionFilter == "notCompleted")
                    query = query.Where(r => r.IsCompleted == false);
            }

            // ✅ فلترة بالتواريخ
            if (fromDate.HasValue)
                query = query.Where(r => r.RequestedDate >= fromDate.Value.Date);
            if (toDate.HasValue)
                query = query.Where(r => r.RequestedDate <= toDate.Value.Date);

            // ✅ ترتيب حسب الحالة
            query = query.OrderBy(r =>
                r.Status == StudySessionStatus.Pending ? 0 :
                r.Status == StudySessionStatus.Rejected ? 1 :
                r.Status == StudySessionStatus.Approved ? 2 : 3);

            var sessions = await query.ToListAsync();

            var statusList = Enum.GetValues(typeof(StudySessionStatus))
                .Cast<StudySessionStatus>()
                .Select(s => new SelectListItem
                {
                    Value = s.ToString(),
                    Text = s.GetDisplayName(),
                    Selected = s.ToString() == statusFilter
                }).ToList();

            statusList.Insert(0, new SelectListItem { Value = "", Text = "-- كل الحالات --" });

            var completionList = new List<SelectListItem>
    {
        new SelectListItem { Value = "", Text = "-- كل الجلسات --", Selected = string.IsNullOrEmpty(completionFilter) },
        new SelectListItem { Value = "completed", Text = "الجلسات المُكتملة", Selected = completionFilter == "completed" },
        new SelectListItem { Value = "notCompleted", Text = "الجلسات غير المُكتملة", Selected = completionFilter == "notCompleted" }
    };

            var viewModel = new SessionFilterViewModel
            {
                StatusFilter = statusFilter,
                StatusList = statusList,
                FromDate = fromDate,
                ToDate = toDate,
                CompletionFilter = completionFilter,
                CompletionList = completionList,
                Sessions = sessions.Select(r => new AdminSessionApprovalViewModel
                {
                    Id = r.Id,
                    StudentName = r.Student.FullName,
                    BranchName = r.Branch?.Name,
                    InstructorName = r.Instructor?.FullName ?? "لم يحدد",
                    RequestedDate = r.RequestedDate,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    TotalSeats = r.TotalSeats,
                    BaseFee = r.BaseFee,
                    IsCompleted = r.IsCompleted,
                    AdditionalServices = r.AdditionalServices,
                    Status = r.Status
                }).ToList()
            };

            return View(viewModel);
        }


        [HttpGet]
        public async Task<IActionResult> MarkAsCompleted(int id)
        {
            var session = await _context.StudySessionReservations.FindAsync(id);
            if (session == null)
                return NotFound();

            session.IsCompleted = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ تم تأكيد إتمام الجلسة بنجاح.";
            return RedirectToAction("Index");
        }


        [HttpPost]
        public async Task<IActionResult> MarkCompleted(int id)
        {
            var session = await _context.StudySessionReservations.FindAsync(id);
            if (session == null)
                return NotFound();

            session.IsCompleted = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ تم تأكيد إتمام الجلسة.";
            return RedirectToAction("Index");
        }


        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var session = await _context.StudySessionReservations
                .Include(r => r.Student)
                .Include(r => r.Branch)
                .Include(r => r.Instructor)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (session == null)
                return NotFound();

            var viewModel = new AdminSessionApprovalViewModel
            {
                Id = session.Id,
                StudentName = session.Student.FullName,
                BranchName = session.Branch?.Name,
                InstructorName = session.Instructor?.FullName ?? "لم يحدد",
                RequestedDate = session.RequestedDate,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                TotalSeats = session.TotalSeats,
                BaseFee = session.BaseFee,
                AdditionalServices = session.AdditionalServices
            };

            return View(viewModel);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AdminSessionApprovalViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            var session = await _context.StudySessionReservations
                .Include(r => r.Instructor)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (session == null)
                return NotFound();

            // تعديل البيانات
            session.RequestedDate = model.RequestedDate;
            session.StartTime = model.StartTime;
            session.EndTime = model.EndTime;
            session.TotalSeats = model.TotalSeats;
            session.BaseFee = model.BaseFee;
            session.IsCompleted = model.IsCompleted;

            session.AdditionalServices = model.AdditionalServices;

            // ملاحظة: لو حابب تربط بالمدرب فعليًا عبر DropDown، تحتاج InstructorId هنا

            _context.Update(session);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم تعديل بيانات الجلسة بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var session = await _context.StudySessionReservations
                .Include(r => r.Student)
                .Include(r => r.Branch)
                .Include(r => r.Instructor)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (session == null)
                return NotFound();

            return View(session);
        }




        public async Task<IActionResult> Approve(int id)
        {
            var session = await _context.StudySessionReservations
                .Include(r => r.Student)
                .Include(r => r.Branch)
                .Include(r => r.Instructor)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (session == null)
                return NotFound();

            var viewModel = new AdminSessionApprovalViewModel
            {
                Id = session.Id,
                StudentName = session.Student.FullName,
                BranchName = session.Branch?.Name,
                InstructorName = session.Instructor?.FullName ?? "لم يحدد",
                RequestedDate = session.RequestedDate,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                TotalSeats = session.TotalSeats,
                BaseFee = session.BaseFee,
                AdditionalServices = session.AdditionalServices
            };

            return View(viewModel);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(AdminSessionApprovalViewModel model)
        {
            var session = await _context.StudySessionReservations.FindAsync(model.Id);
            if (session == null)
                return NotFound();

            session.Status = StudySessionStatus.Approved;
            session.IsApproved = true;
            session.BaseFee = model.BaseFee;
            session.AdditionalServices = model.AdditionalServices;

            _context.Update(session);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تمت الموافقة على الجلسة بنجاح.";
            return RedirectToAction(nameof(Index));
        }




        public async Task<IActionResult> Reject(int id)
        {
            var session = await _context.StudySessionReservations
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (session == null)
                return NotFound();

            ViewBag.StudentName = session.Student.FullName;
            return View(new RejectSessionViewModel { Id = session.Id });
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(RejectSessionViewModel model)
        {
            var session = await _context.StudySessionReservations.FindAsync(model.Id);
            if (session == null)
                return NotFound();

            session.Status = StudySessionStatus.Rejected;
            session.IsApproved = false;
            session.Notes = model.RejectionReason;

            _context.Update(session);
            await _context.SaveChangesAsync();

            TempData["Error"] = "❌ تم رفض الجلسة.";
            return RedirectToAction(nameof(Index));
        }
    }












}

