using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Lesson;
using QdratNew.ViewModels.Question;

namespace QdratNew.Areas.Instructors.Controllers
{
    public class InstructorLessonCompletionsController : BaseInstructorController
    {
        private readonly ApplicationDbContext _context;

        public InstructorLessonCompletionsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
            : base(userManager, scopeService)
        {
            _context = context;
        }

        // ─── GET: عرض نموذج اختيار المؤشرات ─────────────────────
        [HttpGet]
        public async Task<IActionResult> SelectLessons()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            var batchIds = await GetInstructorDirectBatchIdsAsync(instructorId);

            var batches = await _context.Batches
                .AsNoTracking()
                .Where(b => batchIds.Contains(b.Id) && !b.IsDeleted && !b.IsArchived)
                .OrderByDescending(b => b.StartDate)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = $"{b.Name} ({b.Course.Name})"
                })
                .ToListAsync();

            var vm = new LessonCompletionFormViewModel
            {
                BatchList = batches,
                CurriculumList = new List<SelectListItem>(),
                SectionList = new List<SelectListItem>()
            };

            return View(vm);
        }

        // ─── POST: حفظ المؤشرات المختارة والانتقال للتأكيد ──────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCompletedLessons(LessonCompletionFormViewModel model)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            if (string.IsNullOrWhiteSpace(model.CompletedLessonIds))
            {
                TempData["Error"] = "يجب اختيار مؤشر واحد على الأقل.";
                return RedirectToAction(nameof(SelectLessons));
            }

            var lessonIds = model.CompletedLessonIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id.Trim(), out var n) ? n : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (!lessonIds.Any())
            {
                TempData["Error"] = "المؤشرات المختارة غير صالحة.";
                return RedirectToAction(nameof(SelectLessons));
            }

            var batch = await _context.Batches
                .AsNoTracking()
                .Select(b => new { b.Id, b.Name })
                .FirstOrDefaultAsync(b => b.Id == model.SelectedBatchId);

            if (batch == null)
                return NotFound();

            var lessons = await _context.Lessons
                .AsNoTracking()
                .Where(l => lessonIds.Contains(l.Id))
                .Select(l => new { l.Id, l.Title, l.SectionId })
                .ToListAsync();

            var lessonSummaries = new List<LessonSummaryViewModel>();
            var rng = new Random();

            foreach (var lesson in lessons)
            {
                var allReviewedQuestions = await _context.Questions
                    .AsNoTracking()
                    .Where(q => q.LessonId == lesson.Id && q.IsReviewed && !q.IsRejected)
                    .Select(q => new { q.Id, q.Title, q.Difficulty })
                    .ToListAsync();

                var totalQ = await _context.Questions
                    .AsNoTracking()
                    .CountAsync(q => q.LessonId == lesson.Id && !q.IsRejected);

                var reviewedQ = allReviewedQuestions.Count;
                var useCount  = Math.Min(reviewedQ, 5);

                // اختيار عشوائي للأسئلة
                var selected = allReviewedQuestions
                    .OrderBy(_ => rng.Next())
                    .Take(useCount)
                    .Select(q => new QuestionSummaryViewModel
                    {
                        QuestionId = q.Id,
                        Title      = q.Title,
                        Difficulty = q.Difficulty.ToString(),
                        IsReviewed = true
                    })
                    .ToList();

                lessonSummaries.Add(new LessonSummaryViewModel
                {
                    LessonId          = lesson.Id,
                    LessonTitle       = lesson.Title,
                    SectionId         = lesson.SectionId,
                    CurriculumId      = model.SelectedCurriculumId,
                    TotalQuestions    = totalQ,
                    ReviewedQuestions = reviewedQ,
                    QuestionsToUse    = useCount,
                    SelectedQuestions = selected
                });
            }

            var confirmVm = new ConfirmHomeworkViewModel
            {
                BatchId = model.SelectedBatchId,
                BatchName = batch.Name,
                CurriculumId = model.SelectedCurriculumId,
                CompletionTitle = model.CompletionTitle ?? "مؤشرات منتهية",
                Lessons = lessonSummaries
            };

            return View("ConfirmHomework", confirmVm);
        }

        // ─── POST: توليد الواجب وإرساله للطلاب ──────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmHomework(ConfirmHomeworkViewModel model)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            if (model.Lessons == null || !model.Lessons.Any())
            {
                TempData["Error"] = "لا توجد مؤشرات لإرسالها.";
                return View(model);
            }

            var totalQuestions = model.Lessons.Sum(l => l.QuestionsToUse);
            if (!model.ForceGenerateAnyway && totalQuestions == 0)
            {
                TempData["Error"] = "لا يوجد أسئلة لإرسالها. تحقق من أن المؤشرات تحتوي على أسئلة مراجعة.";
                TempData["AllowForceGenerate"] = true;
                return View(model);
            }

            var userId = _userManager.GetUserId(User);

            // ─── إنشاء HomeworkSet ────────────────────────────────
            var homeworkSet = new HomeworkSet
            {
                Title = model.CompletionTitle ?? "واجب منتهية مؤشراته",
                BatchId = model.BatchId,
                CurriculumId = model.CurriculumId > 0 ? model.CurriculumId : null,
                CompletionTitle = model.CompletionTitle ?? "",
                AssignedByUserId = userId,
                CreatedAt = DateTime.Now,
                IsSent = true,
                StartAt = model.StartAt,
                EndAt = model.EndAt
            };

            _context.HomeworkSets.Add(homeworkSet);
            await _context.SaveChangesAsync();

            // ─── ربط المحاور ─────────────────────────────────────
            var distinctSectionIds = model.Lessons
                .Select(l => l.SectionId)
                .Distinct()
                .ToList();

            foreach (var sectionId in distinctSectionIds)
            {
                _context.HomeworkSetSections.Add(new HomeworkSetSection
                {
                    HomeworkSetId = homeworkSet.Id,
                    SectionId = sectionId
                });
            }

            // ─── تسجيل إتمام الدروس في BatchLessonCompletion ──────
            var alreadyCompleted = (await _context.BatchLessonCompletions
                .AsNoTracking()
                .Where(lc => lc.BatchId == model.BatchId)
                .Select(lc => lc.LessonId)
                .ToListAsync())
                .ToHashSet();

            foreach (var lesson in model.Lessons)
            {
                if (!alreadyCompleted.Contains(lesson.LessonId))
                {
                    _context.BatchLessonCompletions.Add(new BatchLessonCompletion
                    {
                        BatchId = model.BatchId,
                        LessonId = lesson.LessonId,
                        CompletionDate = DateTime.Now
                    });
                }
            }

            // ─── إسناد الواجب للطلاب ─────────────────────────────
            var studentIds = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == model.BatchId && e.Status == "Active")
                .Select(e => e.StudentID)
                .ToListAsync();

            foreach (var studentId in studentIds)
            {
                _context.HomeworkSetStudents.Add(new HomeworkSetStudent
                {
                    HomeworkSetId = homeworkSet.Id,
                    StudentId = studentId,
                    AssignedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ تم إرسال الواجب بنجاح لـ {studentIds.Count} طالب.";
            return RedirectToAction(nameof(SentHomeworksReport));
        }

        // ─── GET: تقرير الواجبات المرسلة ─────────────────────────
        [HttpGet]
        public async Task<IActionResult> SentHomeworksReport()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            var batchIds = await GetInstructorDirectBatchIdsAsync(instructorId);
            var userId = _userManager.GetUserId(User);

            var homeworkSets = await _context.HomeworkSets
                .AsNoTracking()
                .Where(h => batchIds.Contains(h.BatchId) && h.AssignedByUserId == userId && h.IsSent)
                .OrderByDescending(h => h.CreatedAt)
                .Select(h => new HomeworkReportViewModel
                {
                    HomeworkSetId = h.Id,
                    Title = h.Title,
                    BatchName = h.Batch != null ? h.Batch.Name : "",
                    CurriculumTitle = h.Curriculum != null ? h.Curriculum.Title : "",
                    CreatedAt = h.CreatedAt
                })
                .ToListAsync();

            return View(homeworkSets);
        }

        // ─── AJAX: معاينة سؤال (Partial HTML) ───────────────────
        [HttpGet]
        public async Task<IActionResult> PreviewQuestion(Guid questionId)
        {
            var question = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .Include(q => q.Curriculum)
                .Include(q => q.Lesson).ThenInclude(l => l.Section)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
                return Content("<div class='text-danger p-3'>السؤال غير موجود</div>", "text/html");

            var model = question.ToDisplayModel();
            while (model.Options.Count < 4)
                model.Options.Add(new QuestionOptionDisplayViewModel());

            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", model);
        }

        // ─── AJAX: استبدال سؤال بآخر من نفس المؤشر ──────────────
        [HttpGet]
        public async Task<IActionResult> GetSwapQuestion(int lessonId, string excludeIds)
        {
            var excluded = (excludeIds ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Guid.TryParse(s.Trim(), out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToHashSet();

            var rng = new Random();

            var candidate = await _context.Questions
                .AsNoTracking()
                .Where(q => q.LessonId == lessonId
                         && q.IsReviewed
                         && !q.IsRejected
                         && !excluded.Contains(q.Id))
                .OrderBy(_ => Guid.NewGuid())
                .Select(q => new { q.Id, q.Title, q.Difficulty })
                .FirstOrDefaultAsync();

            if (candidate == null)
                return Json(new { success = false, message = "لا توجد أسئلة بديلة متاحة" });

            return Json(new
            {
                success    = true,
                questionId = candidate.Id,
                title      = candidate.Title,
                difficulty = candidate.Difficulty.ToString()
            });
        }

        // ─── AJAX: المناهج حسب الدفعة ────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetCurriculaByBatch(int batchId)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            var curriculumIds = await _context.InstructorCurriculumBatches
                .AsNoTracking()
                .Where(icb => icb.InstructorId == instructorId && icb.BatchId == batchId)
                .Select(icb => icb.CurriculumId)
                .Distinct()
                .ToListAsync();

            var curricula = await _context.Curriculums
                .AsNoTracking()
                .Where(c => curriculumIds.Contains(c.Id))
                .Select(c => new { id = c.Id, title = c.Title })
                .OrderBy(c => c.title)
                .ToListAsync();

            return Json(curricula);
        }

        // ─── AJAX: المحاور حسب المنهج ────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetSectionsByCurriculum(int curriculumId)
        {
            var sections = await _context.Sections
                .AsNoTracking()
                .Where(s => s.CurriculumId == curriculumId)
                .OrderBy(s => s.Title)
                .Select(s => new { id = s.Id, title = s.Title })
                .ToListAsync();

            return Json(sections);
        }

        // ─── AJAX: المؤشرات حسب المحور ───────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetLessonsBySection(int sectionId)
        {
            var lessons = await _context.Lessons
                .AsNoTracking()
                .Where(l => l.SectionId == sectionId && l.IsActive)
                .OrderBy(l => l.Title)
                .Select(l => new { id = l.Id, title = l.Title })
                .ToListAsync();

            return Json(lessons);
        }
    }
}
