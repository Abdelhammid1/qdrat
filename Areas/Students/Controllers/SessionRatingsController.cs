using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using Microsoft.AspNetCore.Identity;
using QdratNew.ViewModels.Students;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    public class SessionRatingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SessionRatingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Create(int sessionId)
        {
            var session = await _context.StudySessionReservations
                .Include(s => s.Instructor)
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
                return NotFound();

            // ⚠️ الحصول على StudentId المرتبط بالجلسة مباشرةً
            var studentId = session.StudentID;

            var model = new SessionRatingViewModel
            {
                SessionId = session.Id,
                StudentId = studentId, // ✅ هذا هو المهم
                InstructorName = session.Instructor?.FullName ?? "لم يتم التحديد",
                BranchName = session.Branch?.Name ?? "غير معروف",
                SessionDate = session.RequestedDate
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SessionRatingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // إعادة تعبئة بيانات العرض
                var session = await _context.StudySessionReservations
                    .Include(s => s.Instructor)
                    .Include(s => s.Branch)
                    .FirstOrDefaultAsync(s => s.Id == model.SessionId);

                if (session == null) return NotFound();

                model.InstructorName = session.Instructor?.FullName ?? "غير محدد";
                model.BranchName = session.Branch?.Name;
                model.SessionDate = session.RequestedDate;

                return View(model);
            }

            // إضافة التقييم
            var rating = new StudySessionRating
            {
                SessionId = model.SessionId,
                StudentId = model.StudentId,
                TrainerClarity = model.TrainerClarity,
                TrainerCommitment = model.TrainerCommitment,
                SessionBenefit = model.SessionBenefit,
                Comment = model.Comment,
                RatedAt = DateTime.UtcNow
            };

            _context.StudySessionRatings.Add(rating);

           

            await _context.SaveChangesAsync();

            return RedirectToAction("Dashboard", "StudySessions");
        }

    }
}
