using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Interfaces;
using QdratNew.Services.HomeworkDraft.Interfaces;
using QdratNew.Services.Interfaces;
using QdratNew.Services.PartnerHomework;
using QdratNew.Services.Statistics.Interfaces;
using QdratNew.ViewModels.Partner.Homework;
using QdratNew.ViewModels.Partner.HomeworkDraft;
using QdratNew.ViewModels.Question;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    public class HomeworkDraftsController : PartnerBaseController
    {
        private readonly IHomeworkDraftService _draftService;
        private readonly IPartnerHomeworkAssignmentService _assignmentService;
        private readonly IHomeworkStatisticsService _statsService;
        private readonly ITimeZoneService _timeZoneService;


        public HomeworkDraftsController(
         IHomeworkDraftService draftService,
         IPartnerHomeworkAssignmentService assignmentService,
         IHomeworkStatisticsService statsService, // ✅ هنا
         ITimeZoneService timeZoneService,
         IPartnerSubscriptionService subscriptionService,
         ApplicationDbContext context)
         : base(subscriptionService, context)
        {
            _draftService = draftService;
            _assignmentService = assignmentService;
            _statsService = statsService;
            _timeZoneService = timeZoneService;
        }


        // =========================
        // 📄 Draft List
        // =========================
        public async Task<IActionResult> Index()
        {
            var guard = RequirePermission(SubscriptionContext.CanCreateHomework);
            if (guard != null) return guard;

            var drafts = await _draftService.GetDrafts(
                ActivePartnerId,
                ActiveSubscriptionPeriodId
            );

            ViewBag.Stats = _statsService.GetPartnerDraftStats(
                ActivePartnerId,
                ActiveSubscriptionPeriodId
            );

            return View(drafts);
        }


        [HttpGet]
        public async Task<IActionResult> AddQuestion(int draftId, int lessonId)
        {
            var questions = await _draftService.GetAddCandidates(draftId, lessonId);

            var model = new AddHomeworkDraftQuestionVM
            {
                DraftId = draftId,
                LessonId = lessonId,
                Questions = questions
            };

            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmAddQuestion(int draftId, Guid questionId)
        {
            await _draftService.AddQuestion(draftId, questionId);

            TempData["Success"] = "تمت إضافة السؤال.";
            return RedirectToAction(nameof(Preview), new { id = draftId });
        }




        // =========================
        // 👁️ Preview Draft
        // =========================
        public async Task<IActionResult> Preview(int id)
        {
            var draft = await _draftService.GetDraftForPreview(id);
            if (draft == null)
                return NotFound();

            return View(draft);
        }


        [HttpGet]
        public async Task<IActionResult> ReplaceQuestion(
            int draftId,
            Guid oldQuestionId,
            int lessonId)
        {
            var model = await _draftService.GetReplaceCandidates(
                draftId,
                oldQuestionId,
                lessonId
            );

            return View(model);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReplaceQuestion(
            ReplaceHomeworkDraftQuestionVM model)
        {
            await _draftService.ReplaceQuestion(
                model.DraftId,
                model.OldQuestionId,
                model.NewQuestionId
            );

            TempData["Success"] = "تم استبدال السؤال بنجاح.";
            return RedirectToAction(nameof(Preview), new { id = model.DraftId });
        }


        // =========================================
        // ➕ اختيار نوع التوليد
        // =========================================
        public IActionResult Create()
        {
            var model = new HomeworkDraftCreateVM
            {
                Courses = AllowedCourses
            };

            return View(model);
        }


        [HttpGet]
        public IActionResult AutoGenerate(int courseId)
        {
            var model = new HomeworkAutoGenerateVM
            {
                CourseId = courseId
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoGenerate(HomeworkAutoGenerateVM model)
        {
            // 🔴 تحقق يدوي إضافي
            if (model.QuestionsPerLesson <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.QuestionsPerLesson),
                    "يجب تحديد عدد أسئلة صحيح."
                );
            }

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var draftId = await _draftService.GenerateAutoDraft(
                    ActivePartnerId,
                    ActiveSubscriptionPeriodId,
                    model
                );

                return RedirectToAction(nameof(Preview), new { id = draftId });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }



        [HttpGet]
        public IActionResult GetSections(int curriculumId)
        {
            var sections = _context.Sections
                .Where(s => s.CurriculumId == curriculumId)
                .Select(s => new
                {
                    id = s.Id,
                    name = s.Title
                })
                .OrderBy(s => s.name)
                .ToList();

            return Json(sections);
        }



        [HttpGet]
        public IActionResult GetLessons(int sectionId)
        {
            var lessons = _context.Lessons
                .Where(l =>
                    l.SectionId == sectionId &&
                    l.IsActive)
                .Select(l => new
                {
                    l.Id,
                    l.Title
                })
                .OrderBy(l => l.Title)
                .ToList();

            return Json(lessons);
        }




        [HttpGet]
        public IActionResult GetActiveLessons(int sectionId)
        {
            var lessons = _context.Lessons
                .Where(l =>
                    l.SectionId == sectionId &&
                    l.IsActive)
                .Select(l => new
                {
                    id = l.Id,
                    title = l.Title
                })
                .OrderBy(l => l.title)
                .ToList();

            return Json(lessons);
        }



        [HttpGet]
        public IActionResult GetCurriculums(int courseId)
        {
            var curriculums = _context.CourseCurriculums
                .Where(cc => cc.CourseId == courseId)
                .Select(cc => new
                {
                    id = cc.Curriculum.Id,
                    name = cc.Curriculum.Title
                })
                .Distinct()
                .OrderBy(x => x.name)
                .ToList();

            return Json(curriculums);
        }






        // =========================
        // 👁️ Preview Question (Modal)
        // =========================
        [HttpGet]
        public async Task<IActionResult> PreviewQuestion(Guid questionId)
        {
            var question = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
                return Content("<div class='text-danger'>❌ السؤال غير موجود</div>");

            var model = question.ToDisplayModel();
            model.VerbalPassageContent = question.VerbalPassage?.Content;

            while (model.Options.Count < 4)
            {
                model.Options.Add(new QuestionOptionDisplayViewModel());
            }

            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", model);
        }

        // =========================
        // 🚀 Send Draft
        // =========================
        [HttpGet]
        public IActionResult SendDraft(int draftId)
        {
            var nowRaw = _timeZoneService.GetNowSaudi();

            var now = new DateTime(
                nowRaw.Year,
                nowRaw.Month,
                nowRaw.Day,
                nowRaw.Hour,
                nowRaw.Minute,
                0
            );

            var model = new SendHomeworkDraftVM
            {
                DraftId = draftId,
                StartAt = now,
                EndAt = now.AddDays(1),
                Batches = _context.Batches
                    .Where(b => b.Branch.PartnerId == ActivePartnerId)
                    .Select(b => new SelectListItem
                    {
                        Value = b.Id.ToString(),
                        Text = b.Name
                    })
                    .ToList()
            };

            return View(model);
        }

        // =====================================================
        // 🗑 حذف مسودة واجب (مطابق للكيانات الفعلية)
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDraft(int draftId)
        {
            if (draftId <= 0)
                return BadRequest();

            // 1️⃣ تحقق الملكية
            var exists = await _context.HomeworkDrafts
                .AsNoTracking()
                .AnyAsync(d =>
                    d.Id == draftId &&
                    d.PartnerId == ActivePartnerId &&
                    d.SubscriptionPeriodId == ActiveSubscriptionPeriodId);

            if (!exists)
                return Forbid();

            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction =
                        await _context.Database.BeginTransactionAsync();

                    // حذف أسئلة المسودة
                    await _context.HomeworkDraftQuestions
                        .Where(q => q.HomeworkDraftId == draftId)
                        .ExecuteDeleteAsync();

                    // حذف المسودة نفسها
                    await _context.HomeworkDrafts
                        .Where(d => d.Id == draftId)
                        .ExecuteDeleteAsync();

                    await transaction.CommitAsync();
                });

                TempData["Success"] = "تم حذف المسودة بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "فشل الحذف: " + ex.GetBaseException().Message;
            }

            return RedirectToAction(nameof(Index));
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendDraft(SendHomeworkDraftVM model)
        {
            if (model.BatchIds == null || !model.BatchIds.Any())
            {
                ModelState.AddModelError(
                    nameof(model.BatchIds),
                    "يجب اختيار دفعة واحدة على الأقل."
                );

                model.Batches = _context.Batches
                    .Where(b => b.Branch.PartnerId == ActivePartnerId)
                    .Select(b => new SelectListItem
                    {
                        Value = b.Id.ToString(),
                        Text = b.Name
                    })
                    .ToList();

                return View(model);
            }

            // 🔹 إزالة المللي ثانية
            if (model.StartAt.HasValue)
                model.StartAt = model.StartAt.Value.AddMilliseconds(-model.StartAt.Value.Millisecond);
            model.EndAt = model.EndAt.AddMilliseconds(-model.EndAt.Millisecond);

            // 🔹 تحقق منطقي
            if (model.StartAt.HasValue && model.EndAt <= model.StartAt)
            {
                ModelState.AddModelError(nameof(model.EndAt),
                    "تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء.");

                model.Batches = _context.Batches
                    .Where(b => b.Branch.PartnerId == ActivePartnerId)
                    .Select(b => new SelectListItem
                    {
                        Value = b.Id.ToString(),
                        Text = b.Name
                    })
                    .ToList();

                return View(model);
            }

            // 🔹 تحويل إلى UTC (نفس الأدمن)
            model.StartAt = model.StartAt.HasValue ? _timeZoneService.ConvertToUtc(model.StartAt.Value) : (DateTime?)null;
            model.EndAt = _timeZoneService.ConvertToUtc(model.EndAt);

            _assignmentService.SendDraftToBatches(
                model,
                ActivePartnerId,
                ActiveSubscriptionPeriodId
            );

            TempData["Success"] = "تم إرسال الواجب.";
            return RedirectToAction(nameof(Index));
        }



    }
}
