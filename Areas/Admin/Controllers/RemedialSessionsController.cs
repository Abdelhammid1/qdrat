using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Remedial;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class RemedialSessionsController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IRemedialTrackingService _trackingService;

        public RemedialSessionsController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IRemedialTrackingService trackingService)
        {
            _contextFactory = contextFactory;
            _trackingService = trackingService;
        }

        // 🟢 1. عرض جميع الجلسات العلاجية
        public async Task<IActionResult> Index()
        {
            using var _context = _contextFactory.CreateDbContext();

            var sessions = await (
                from s in _context.RemedialSessions
                    .Include(s => s.Student)
                    .Include(s => s.RemedialPlan)
                orderby s.CreatedAt descending
                select new RemedialSessionSummaryVm
                {
                    Id = s.Id,
                    StudentId = s.StudentID,
                    StudentName = s.Student.FullName,
                    PlanTitle = s.RemedialPlan.Title,
                    CreatedAt = s.CreatedAt,
                    IsConfirmed = s.IsConfirmed,
                    IsCompleted = s.IsCompleted,
                    RemedialPlanId = s.RemedialPlanId,

                    // ✅ تحديد أول محور من الدروس العلاجية
                    SectionId = _context.RemedialLessons
                        .Where(rl => rl.RemedialPlanId == s.RemedialPlanId)
                        .Select(rl => rl.SectionId)
                        .FirstOrDefault(),

                    // ✅ عدد الأنشطة العلاجية (بدل مدة المشاهدة)
                    WatchedMinutes = _context.RemedialPlanInteractions
                        .Count(r => r.RemedialPlanId == s.RemedialPlanId),

                    // ✅ متوسط نتيجة الكويزات من خلال الكويزات المرتبطة بالخطة
                    AverageScore = _context.StudentRemedialQuizResults
                        .Where(q =>
                            _context.RemedialQuizzes
                                .Where(rq => rq.RemedialPlanId == s.RemedialPlanId)
                                .Select(rq => rq.Id)
                                .Contains(q.RemedialQuizId))
                        .Average(q => (double?)q.Score) ?? 0
                }
            ).ToListAsync();

            return View(sessions);
        }


        // 🟢 2. إنشاء جلسة علاجية جديدة لطالب معين
        [HttpGet]
        public async Task<IActionResult> Create(int? studentId, int? planId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // تحميل جميع الطلاب
            var students = await _context.Students
                .Select(s => new SelectListItem
                {
                    Value = s.StudentID.ToString(),
                    Text = s.FullName
                }).ToListAsync();

            // تحميل جميع الخطط غير المكتملة
            var plans = await _context.RemedialPlans
                .Include(p => p.Student)
                .Where(p => p.IsCompleted == false)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Title} - ({p.Student.FullName})"
                }).ToListAsync();

            // 🟢 تحديد الطالب المختار (إن وجد)
            foreach (var s in students)
            {
                if (studentId.HasValue && s.Value == studentId.Value.ToString())
                    s.Selected = true;
            }

            // 🟢 تحديد الخطة المختارة (إن وجدت)
            foreach (var p in plans)
            {
                if (planId.HasValue && p.Value == planId.Value.ToString())
                    p.Selected = true;
            }

            var vm = new RemedialSessionCreateVm
            {
                Students = students,
                Plans = plans,
                StudentId = studentId ?? 0,
                RemedialPlanId = planId ?? 0
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RemedialSessionCreateVm model)
        {
            if (!ModelState.IsValid)
            {
                using var ctx = _contextFactory.CreateDbContext();
                model.Students = await ctx.Students
                    .Select(s => new SelectListItem
                    {
                        Value = s.StudentID.ToString(),
                        Text = s.FullName
                    }).ToListAsync();

                model.Plans = await ctx.RemedialPlans
                    .Include(p => p.Student)
                    .Where(p => p.IsCompleted == false)
                    .Select(p => new SelectListItem
                    {
                        Value = p.Id.ToString(),
                        Text = $"{p.Title} - ({p.Student.FullName})"
                    }).ToListAsync();

                return View(model);
            }

            using var _context = _contextFactory.CreateDbContext();

            var session = new RemedialSession
            {
                StudentID = model.StudentId,
                RemedialPlanId = model.RemedialPlanId,
                ScheduledDate = model.ScheduledAt,
                CreatedAt = DateTime.Now,
                IsConfirmed = false,
                AccessCode = new Random().Next(100000, 999999).ToString(),

                // ✅ الحل: تعيين قيمة افتراضية للـ Resources
                Resources = string.Empty
            };

            _context.RemedialSessions.Add(session);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم إنشاء الجلسة العلاجية بنجاح.";
            return RedirectToAction("Index");
        }




        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var session = await _context.RemedialSessions
                .Include(r => r.Student)
                .Include(r => r.RemedialPlan)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (session == null)
                return NotFound();

            var vm = new RemedialSessionEditVm
            {
                Id = session.Id,
                StudentName = session.Student.FullName,
                PlanTitle = session.RemedialPlan.Title,
                ScheduledDate = session.ScheduledDate,
                IsConfirmed = session.IsConfirmed
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(RemedialSessionEditVm model)
        {
            using var _context = _contextFactory.CreateDbContext();

            var session = await _context.RemedialSessions.FindAsync(model.Id);
            if (session == null)
                return NotFound();

            session.ScheduledDate = model.ScheduledDate ?? DateTime.Now;
            session.IsConfirmed = model.IsConfirmed;
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم تحديث الجلسة العلاجية بنجاح.";
            return RedirectToAction(nameof(Index));
        }



        [HttpPost]
        public async Task<IActionResult> ConfirmAttendance(int id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var session = await _context.RemedialSessions.FindAsync(id);
            if (session == null)
                return Json(new { success = false, message = "❌ لم يتم العثور على الجلسة." });

            session.IsConfirmed = true;
            session.ConfirmedAt = DateTime.Now;  // تأكيد الحضور
            session.CompletedAt = DateTime.Now;  // إنهاء الجلسة

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "✅ تم تأكيد حضور الطالب للجلسة." });
        }



        [HttpPost]
        public async Task<IActionResult> CompleteSession(int id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var session = await _context.RemedialSessions.FindAsync(id);
            if (session == null)
                return Json(new { success = false, message = "❌ لم يتم العثور على الجلسة." });

            session.IsCompleted = true;
            session.CompletedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "✅ تم إنهاء الجلسة العلاجية." });
        }


        [HttpGet]
        public async Task<IActionResult> ProgressReport(int id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var session = await _context.RemedialSessions
                .Include(s => s.Student)
                .Include(s => s.RemedialPlan)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null)
                return NotFound();

            var logs = await _context.RemedialSessionLogs
                .Where(l => l.SessionId == id)
                .ToListAsync();

            var avgWatch = logs.Any()
                ? Math.Round(logs.Average(l => l.DurationWatched) / 60.0, 1)
                : 0.0;
            var completedVideos = logs.Count(l => l.IsCompleted);

            var report = new RemedialSessionProgressVm
            {
                SessionId = id,
                StudentName = session.Student.FullName,
                PlanTitle = session.RemedialPlan.Title,
                AverageWatchMinutes = avgWatch,
                CompletedVideos = completedVideos
            };

            return View(report);
        }


        // 🟡 تحميل الخطط العلاجية الخاصة بالطالب (Ajax)
        [HttpGet]
        public async Task<IActionResult> GetPlansByStudent(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var plans = await _context.RemedialPlans
        .Where(p => p.StudentID == studentId && (p.IsCompleted == false || p.IsCompleted == null))
        .Select(p => new
        {
            p.Id,
            p.Title
        })
        .ToListAsync();


            return Json(plans);
        }

        // 🟢 تحميل المحاور (Sections) الخاصة بالخطة العلاجية (Ajax)
        [HttpGet]
        public async Task<IActionResult> GetWeakSectionsByPlan(int planId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var sections = await (
                from lesson in _context.RemedialLessons
                join section in _context.Sections on lesson.SectionId equals section.Id
                where lesson.RemedialPlanId == planId
                select new
                {
                    section.Id,
                    section.Title
                }
            ).Distinct().ToListAsync();

            return Json(sections);
        }






        // 🟢 3. تفاصيل الجلسة (عرض الكود المرجعي، الحالة، مدة المشاهدة)
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var session = await _context.RemedialSessions
                .Include(s => s.Student)
                .Include(s => s.RemedialPlan)
                .ThenInclude(p => p.Lessons)
                .ThenInclude(l => l.Section)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null)
            {
                TempData["ErrorMessage"] = "⚠️ لم يتم العثور على الجلسة.";
                return RedirectToAction("Index");
            }

            // 🧩 توليد كود مرجعي إذا لم يكن موجودًا
            if (string.IsNullOrEmpty(session.AccessCode))
            {
                session.AccessCode = new Random().Next(100000, 999999).ToString();
                _context.Update(session);
                await _context.SaveChangesAsync();
            }

            // 🔹 تحميل بيانات التتبع (مدة المشاهدة، نتائج التدريب)
            var trackingData = await _trackingService.GetSessionTrackingSummaryAsync(session.Id);

            var vm = new RemedialSessionDetailsVm
            {
                SessionId = session.Id,
                StudentName = session.Student.FullName,
                PlanTitle = session.RemedialPlan.Title,
                AccessCode = session.AccessCode,
                CreatedAt = session.CreatedAt,
                TotalWatchedSeconds = trackingData.SecondsWatched,
                TotalQuizAttempts = trackingData.QuizzesCompleted,
                AverageScore = trackingData.AverageScore
            };

            return View(vm);
        }

        // 🟢 4. تسجيل تقدم المشاهدة
        [HttpPost]
        public async Task<IActionResult> LogVideoProgress([FromBody] VideoWatchModel model)
        {
            if (model == null || model.StudentId == 0)
                return Json(new { success = false, message = "❌ بيانات غير صالحة." });

            await _trackingService.LogVideoWatchAsync(model.SessionId, model.VideoId, model.SecondsWatched);
            return Json(new { success = true, message = "✅ تم تسجيل المشاهدة." });
        }

        // 🟢 5. حفظ نتيجة التدريب بعد الفيديو
        [HttpPost]
        public async Task<IActionResult> SubmitQuizResult([FromBody] QuizResultModel model)
        {
            if (model == null || model.StudentId == 0)
                return Json(new { success = false, message = "❌ بيانات غير صالحة." });

            await _trackingService.SaveQuizAttemptAsync(model.StudentId, model.VideoId, model.QuizId, model.IsCorrect);
            return Json(new { success = true, message = "✅ تم حفظ نتيجة التدريب." });
        }

        // 🟢 6. حذف جلسة
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            using var _context = _contextFactory.CreateDbContext();
            var session = await _context.RemedialSessions.FindAsync(id);

            if (session == null)
                return Json(new { success = false, message = "لم يتم العثور على الجلسة." });

            _context.RemedialSessions.Remove(session);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "🗑️ تم حذف الجلسة بنجاح." });
        }
    }
}
