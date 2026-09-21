using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Areas.Parents.Controllers.Base;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Parents.Interfaces;
using QdratNew.ViewModels.Parents;
using System.Threading.Tasks;

namespace QdratNew.Areas.Parents.Controllers
{
    public class SmartPracticeController : ParentBaseController
    {
        private readonly IParentSmartPracticeService _practiceService;

        public SmartPracticeController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IParentAccessService parentAccessService,
            IParentSmartPracticeService practiceService)
            : base(contextFactory, userManager, parentAccessService)
        {
            _practiceService = practiceService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? studentId)
        {
            int sid = studentId ?? SelectedStudentId ?? 0;
            using var db = _contextFactory.CreateDbContext();

            var children = await db.Students
                .AsNoTracking()
                .Where(s => s.ParentId == ParentId)
                .Select(s => new ParentChildCardViewModel { StudentId = s.StudentID, StudentName = s.FullName })
                .ToListAsync();

            var requests = await db.Set<ParentSmartPracticeRequest>()
                .AsNoTracking()
                .Where(r => r.ParentId == ParentId && (sid == 0 || r.StudentId == sid))
                .Join(db.Students, r => r.StudentId, s => s.StudentID,
                    (r, s) => new ParentSmartPracticeDetailsViewModel
                    {
                        RequestId = r.Id,
                        StudentName = s.FullName,
                        PracticeMode = r.PracticeMode,
                        PracticeModeLabel = GetModeLabel(r.PracticeMode),
                        Status = r.Status,
                        StatusLabel = GetStatusLabel(r.Status),
                        StatusColor = r.Status == "Active" ? "success" : "secondary",
                        QuestionCount = r.RequestedQuestionCount,
                        DurationMinutes = r.RequestedDurationMinutes,
                        CreatedAt = r.CreatedAt,
                        SafeSummary = r.SafeSummary ?? string.Empty
                    })
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.StudentId = sid;
            ViewBag.Children = children;
            return View(requests);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? studentId)
        {
            var userId = _userManager.GetUserId(User)!;
            var vm = await _practiceService.BuildCreateModelAsync(userId, studentId ?? SelectedStudentId);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ParentSmartPracticeCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var userId2 = _userManager.GetUserId(User)!;
                var rebuilt = await _practiceService.BuildCreateModelAsync(userId2, model.StudentId);
                model.AvailableStudents = rebuilt.AvailableStudents;
                model.AvailableCurriculums = rebuilt.AvailableCurriculums;
                return View(model);
            }

            var userId = _userManager.GetUserId(User)!;
            var result = await _practiceService.CreateSmartPracticeAsync(userId, model);

            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(Create), new { studentId = model.StudentId });
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id = result.RequestId });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var vm = await _practiceService.GetDetailsAsync(id, ParentId);
            if (vm == null) return NotFound();
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetSections(int curriculumId)
        {
            using var db = _contextFactory.CreateDbContext();
            var sections = await db.Sections
                .AsNoTracking()
                .Where(s => s.CurriculumId == curriculumId)
                .Select(s => new { id = s.Id, title = s.Title })
                .ToListAsync();
            return Json(sections);
        }

        private static string GetModeLabel(string mode) => mode switch
        {
            "WeaknessBased" => "تعزيز نقاط الضعف",
            "Review" => "مراجعة عامة",
            "Challenge" => "تحدٍّ",
            "ExamPreparation" or "ExamPrep" => "استعداد لاختبار",
            "QuickPractice" => "تدريب سريع",
            _ => mode
        };

        private static string GetStatusLabel(string status) => status switch
        {
            "Active" => "نشط",
            "Pending" => "قيد المعالجة",
            "Completed" => "مكتمل",
            _ => status
        };
    }
}
