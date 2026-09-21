using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.HomeworkEngine.Instructor;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Instructor.Homework;

namespace QdratNew.Areas.Instructors.Controllers
{
    public class HomeworkController : BaseInstructorController
    {
        private readonly ApplicationDbContext _context;
        private readonly IInstructorHomeworkEngineService _engine;

        public HomeworkController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService,
            IInstructorHomeworkEngineService engine
        ) : base(userManager, scopeService)
        {
            _context = context;
            _engine = engine;
        }

        // ======================================================
        // صفحة إنشاء واجب
        // ======================================================
        public async Task<IActionResult> Create()
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId == 0)
                return Unauthorized();

            var vm = new CreateInstructorHomeworkVm();

            // ===============================
            // 1️⃣ المناهج الخاصة بالمدرب
            // ===============================
            var instructorCurriculumIds = await _context.CurriculumInstructors
                .Where(ci => ci.InstructorId == instructorId)
                .Select(ci => ci.CurriculumId)
                .ToListAsync();

            // ===============================
            // 2️⃣ ربط الدورات بالمناهج
            // ===============================
            var courseCurriculums = await _context.CourseCurriculums
                .AsNoTracking()
                .ToListAsync();

            var courseIds = courseCurriculums
                .Where(cc => instructorCurriculumIds.Contains(cc.CurriculumId))
                .Select(cc => cc.CourseId)
                .Distinct()
                .ToList();

            // ===============================
            // 3️⃣ جلب الدفعات المرتبطة بالدورات
            // ===============================
            var batches = await _context.Batches
                .AsNoTracking()
                .Where(b => !b.IsDeleted)
                .ToListAsync();

            vm.AvailableBatches = batches
                .Where(b => courseIds.Contains(b.CourseId))
                .Select(b => b.Id)
                .ToList();

            return View(vm);
        }

        // ======================================================
        // إنشاء الواجب
        // ======================================================
        [HttpPost]
        public async Task<IActionResult> Create(CreateInstructorHomeworkVm model)
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId == 0)
                return Unauthorized();

            if (!ModelState.IsValid)
                return View(model);

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            await _engine.CreateHomeworkForBatchAsync(
                model.BatchId,
                model.Title,
                model.QuestionIds,
                model.StartAt,
                model.EndAt,
                userId
            );

            TempData["Success"] = "تم إنشاء الواجب بنجاح";

            return RedirectToAction(nameof(Create));
        }
    }
}