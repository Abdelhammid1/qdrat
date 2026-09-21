using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Implementations;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Remedial;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class RemedialPlansController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITimeZoneService _timeZoneService;
        private readonly IPerformanceIndicatorAnalysisService _analysisService;

        public RemedialPlansController(ApplicationDbContext context, ITimeZoneService timeZoneService, IPerformanceIndicatorAnalysisService analysisService)
        {
            _context = context;
            _timeZoneService = timeZoneService;
            _analysisService = analysisService;
        }

        // ✅ شاشة إنشاء الخطة
        [HttpGet]
        public async Task<IActionResult> Create(int studentId)
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null) return NotFound();

            var weakSections = await _context.StudentPerformances
                .Where(p => p.StudentID == studentId && p.Score < 50)
                .Include(p => p.Section)
                .Select(p => new SelectListItem
                {
                    Value = p.SectionId.ToString(),
                    Text = p.Section.Title + $" (الدرجة: {p.Score})"
                }).ToListAsync();

            var viewModel = new RemedialPlanCreateViewModel
            {
                StudentId = studentId,
                StudentName = student.FullName,
                Title = $"خطة علاجية للطالب {student.FullName}",
                Description = "خطة علاجية بناءً على نقاط الضعف المكتشفة.",
                PerformanceLevel = "ضعيف",
                Sections = weakSections,
                Questions = new List<SelectListItem>() // هنضيفها من بنك الأسئلة لاحقاً
            };

            return View(viewModel);
        }

        // ✅ حفظ الخطة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RemedialPlanCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var plan = new RemedialPlan
            {
                StudentID = model.StudentId,
                Title = model.Title,
                Description = model.Description,
                CreatedAt = _timeZoneService.GetNowUtc(),

                IsCompleted = false,
                PerformanceLevel = model.PerformanceLevel
            };

            _context.RemedialPlans.Add(plan);
            await _context.SaveChangesAsync();

            // ✅ إضافة الدروس العلاجية المرتبطة بالمؤشرات الضعيفة
            if (model.SelectedSectionIds != null && model.SelectedSectionIds.Any())
            {
                foreach (var sectionId in model.SelectedSectionIds)
                {
                    var remedialLesson = new RemedialLesson
                    {
                        RemedialPlanId = plan.Id,
                        SectionId = sectionId,
                        VideoUrl = model.VideoUrls != null && model.VideoUrls.ContainsKey(sectionId)
                                    ? model.VideoUrls[sectionId]
                                    : "",
                        SupplementaryMaterial = "مواد مساعدة"
                    };
                    _context.RemedialLessons.Add(remedialLesson);
                }
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "✅ تم إنشاء الخطة العلاجية بنجاح.";
            return RedirectToAction("Details", "Students", new { area = "Admin", id = model.StudentId });
        }


        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var plans = await (
                from plan in _context.RemedialPlans
                    .Include(p => p.Student)
                    .Include(p => p.TriggerExam)
                        .ThenInclude(e => e.Curriculum)
                    .Include(p => p.Lessons)
                        .ThenInclude(l => l.Section)
                            .ThenInclude(s => s.Curriculum)
                orderby plan.CreatedAt descending
                select new RemedialPlanListVm
                {
                    Id = plan.Id,
                    Title = plan.Title,
                    StudentName = plan.Student.FullName,
                    StudentID = plan.StudentID, // ✅ أضف هذا السطر

                    CurriculumTitle = plan.TriggerExam != null
                        ? plan.TriggerExam.Curriculum.Title
                        : plan.Lessons
                            .Select(l => l.Section.Curriculum.Title)
                            .FirstOrDefault(),

                    // ✅ المحاور الضعيفة المستخرجة من الأسئلة الفعلية
                    WeakSectionDetails = _context.QuestionAttemptNew
                        .Include(x => x.Question)
                            .ThenInclude(qq => qq.Lesson)
                                .ThenInclude(l => l.Section)
                        .Where(q => q.StudentId == plan.StudentID
                                    && q.PerformanceIndicatorExamId == plan.TriggerExamId
                                    && q.Question.Lesson.Section != null)
                        .AsEnumerable() // 🔹 نحول إلى تنفيذ داخل الذاكرة
                        .GroupBy(q => q.Question.Lesson.Section)
                        .Select(g => new WeakSectionDetailVm
                        {
                            SectionTitle = g.Key.Title,
                            Score = g.Count() == 0
                                ? 0
                                : Math.Round((g.Count(x => x.IsCorrect) / (double)g.Count()) * 100, 1)
                        })
                        .Where(x => x.Score < 50)
                        .OrderBy(x => x.Score)
                        .ToList(),

                    PerformanceLevel = plan.PerformanceLevel,
                    IsCompleted = plan.IsCompleted ?? false,
                    CreatedAt = plan.CreatedAt
                }
            ).ToListAsync();

            return View(plans);
        }


        // ✅ عرض تفاصيل خطة محددة
        public async Task<IActionResult> Details(int id)
        {
            var plan = await _context.RemedialPlans
                .Include(p => p.Student)
                .Include(p => p.Lessons).ThenInclude(l => l.Section)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (plan == null)
                return NotFound("❌ لم يتم العثور على الخطة العلاجية.");

            return View(plan);
        }

        // ✅ تعديل البيانات الأساسية للخطة
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var plan = await _context.RemedialPlans.FindAsync(id);
            if (plan == null)
                return NotFound();

            var vm = new RemedialPlanEditViewModel
            {
                Id = plan.Id,
                Title = plan.Title,
                Description = plan.Description,
                PerformanceLevel = plan.PerformanceLevel,
                Recommendations = plan.Recommendations
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(RemedialPlanEditViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var plan = await _context.RemedialPlans.FindAsync(model.Id);
            if (plan == null)
                return NotFound();

            plan.Title = model.Title;
            plan.Description = model.Description;
            plan.PerformanceLevel = model.PerformanceLevel;
            plan.Recommendations = model.Recommendations;
            plan.UpdatedAt = _timeZoneService.GetNowUtc();

            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ تم تعديل الخطة العلاجية بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // ✅ توليد خطة علاجية تلقائيًا بناء على اختبار محدد (عند الحاجة)
        [HttpPost]
        public async Task<IActionResult> GenerateFromExam(int examId)
        {
            try
            {
                await _analysisService.GenerateRemedialPlansAsync(examId);
                TempData["Success"] = "✅ تم توليد الخطط العلاجية بناءً على اختبار المؤشر المحدد.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"⚠️ حدث خطأ أثناء التوليد: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ✅ حذف خطة علاجية
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var plan = await _context.RemedialPlans.FindAsync(id);
            if (plan == null)
                return Json(new { success = false, message = "❌ لم يتم العثور على الخطة." });

            _context.RemedialPlans.Remove(plan);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "🗑 تم حذف الخطة العلاجية بنجاح." });
        }

    }
}
