using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.HomeworkEngine.Assignment;
using QdratNew.Services.HomeworkEngine.Drafts;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Partner;
using QdratNew.ViewModels.Partner.Homework;
using QdratNew.ViewModels.Partner.HomeworkDraft;
using QdratNew.ViewModels.Question;
using System.Security.Claims;

namespace QdratNew.Areas.Instructors.Controllers
{
    [Area("Instructors")]
    public class InstructorHomeworkDraftsController : BaseInstructorController
    {
        private readonly IHomeworkDraftEngineService _draftService;
        private readonly IHomeworkEngineAssignmentService _assignmentService;
        private readonly ApplicationDbContext _context;

        public InstructorHomeworkDraftsController(
            IHomeworkDraftEngineService draftService,
            IHomeworkEngineAssignmentService assignmentService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService
        ) : base(userManager, scopeService)
        {
            _draftService = draftService;
            _assignmentService = assignmentService;
            _context = context;
        }

        // ======================================================
        // Draft List
        // ======================================================
        public async Task<IActionResult> Index()
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId == 0)
                return Unauthorized();

            var drafts = _draftService.GetDrafts(instructorId);

            return View(drafts);
        }




        // =========================
        // Preview Question (Modal)
        // =========================
        [HttpGet]
        public async Task<IActionResult> PreviewQuestion(Guid questionId)
        {
            var question = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
                return Content("<div class='text-danger'>السؤال غير موجود</div>");

            var model = question.ToDisplayModel();
            model.VerbalPassageContent = question.VerbalPassage?.Content;

            while (model.Options.Count < 4)
            {
                model.Options.Add(new QuestionOptionDisplayViewModel());
            }

            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", model);
        }




        public async Task<IActionResult> Archived()
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId == 0)
                return Unauthorized();

            var drafts = await _context.HomeworkDrafts
                .Include(d => d.Questions)
                .Where(d =>
                    d.PartnerId == instructorId &&
                    d.IsArchived &&
                    !d.IsDeleted)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return View(drafts);
        }




        [HttpPost]
        public IActionResult Archive(int id)
        {
            var draft = _context.HomeworkDrafts
                .FirstOrDefault(x => x.Id == id);

            if (draft == null)
                return NotFound();

            draft.IsArchived = true;
            draft.ArchivedAt = DateTime.UtcNow;

            _context.SaveChanges();

            TempData["Success"] = "تم أرشفة المسودة";

            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        public IActionResult Restore(int id)
        {
            var draft = _context.HomeworkDrafts
                .FirstOrDefault(x => x.Id == id);

            if (draft == null)
                return NotFound();

            draft.IsArchived = false;
            draft.ArchivedAt = null;

            _context.SaveChanges();

            TempData["Success"] = "تم استرجاع المسودة";

            return RedirectToAction(nameof(Index));
        }



        [HttpPost]
        public IActionResult Delete(int id)
        {
            var draft = _context.HomeworkDrafts
                .FirstOrDefault(x => x.Id == id);

            if (draft == null)
                return NotFound();

            draft.IsDeleted = true;

            _context.SaveChanges();

            TempData["Success"] = "تم حذف المسودة";

            return RedirectToAction(nameof(Index));
        }







        // ======================================================
        // Get Curriculums
        // ======================================================
        [HttpGet]
        public async Task<IActionResult> GetCurriculums(int courseId)
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
                return Json(new List<object>());

            var hasAccess = await InstructorHasAccessToCourseAsync(instructorId, courseId);

            if (!hasAccess)
                return Json(new List<object>());

            var curriculums = await _context.CourseCurriculums
                .AsNoTracking()
                .Where(cc => cc.CourseId == courseId)
                .Select(cc => new
                {
                    id = cc.Curriculum.Id,
                    name = cc.Curriculum.Title
                })
                .Distinct()
                .OrderBy(x => x.name)
                .ToListAsync();

            return Json(curriculums);
        }


        // ======================================================
        // Get Sections
        // ======================================================
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


        // ======================================================
        // Get Active Lessons
        // ======================================================
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





        // ======================================================
        // Create Draft
        // ======================================================
        public async Task<IActionResult> Create()
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
                return Forbid();

            var allowedCourseIds = await GetInstructorAllowedCourseIdsAsync(instructorId);

            var model = new HomeworkDraftCreateVM();

            var courses = await _context.Courses
                .AsNoTracking()
                .Where(c => c.IsActive)
                .Select(c => new PartnerCourseContext
                {
                    CourseId = c.Id,
                    CourseName = c.Name
                })
                .ToListAsync();

            model.Courses = courses
                .Where(c => allowedCourseIds.Contains(c.CourseId))
                .OrderBy(c => c.CourseName)
                .ToList();

            return View(model);
        }

        // ======================================================
        // Auto Generate Draft
        // ======================================================
        [HttpGet]
        public async Task<IActionResult> AutoGenerate(int courseId)
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
                return Forbid();

            var hasAccess = await InstructorHasAccessToCourseAsync(instructorId, courseId);

            if (!hasAccess)
                return Forbid();

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
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
                return Forbid();

            var hasAccess = await InstructorHasAccessToCourseAsync(instructorId, model.CourseId);

            if (!hasAccess)
                return Forbid();

            if (!ModelState.IsValid)
                return View(model);

            if (model.LessonQuestionCounts == null || !model.LessonQuestionCounts.Any())
            {
                ModelState.AddModelError("", "يجب تحديد عدد الأسئلة لكل مؤشر.");
                return View(model);
            }
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                throw new Exception(string.Join(" | ", errors));
            }

            try
            {
                var draftId = _draftService.GenerateAutoDraft(
                    instructorId,
                    null,
                    model
                );

                return RedirectToAction(nameof(Preview), new { id = draftId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        // ======================================================
        // Preview Draft
        // ======================================================
        public IActionResult Preview(int id)
        {
            var draft = _draftService.GetDraftForPreview(id);

            if (draft == null)
                return NotFound();

            return View(draft);
        }

        // ======================================================
        // Add Question
        // ======================================================
        [HttpGet]
        public IActionResult AddQuestion(int draftId, int lessonId)
        {
            var candidates = _draftService.GetAddCandidates(draftId, lessonId);

            var model = new AddHomeworkDraftQuestionVM
            {
                DraftId = draftId,
                LessonId = lessonId,
                Questions = candidates.Select(q => new ReplaceCandidateQuestionVM
                {
                    QuestionId = q.Id,
                    Title = q.Title,
                    LessonId = q.LessonId
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmAddQuestion(int draftId, Guid questionId)
        {
            _draftService.AddQuestion(draftId, questionId);

            TempData["Success"] = "تمت إضافة السؤال";

            return RedirectToAction(nameof(Preview), new { id = draftId });
        }

        // ======================================================
        // Replace Question
        // ======================================================
        [HttpGet]
        public IActionResult ReplaceQuestion(
          int draftId,
          Guid oldQuestionId,
          int lessonId)
        {
            var model = _draftService.GetReplaceCandidates(
                draftId,
                oldQuestionId,
                lessonId
            );

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmReplaceQuestion(
            ReplaceHomeworkDraftQuestionVM model)
        {
            _draftService.ReplaceQuestion(
                model.DraftId,
                model.OldQuestionId,
                model.NewQuestionId
            );

            TempData["Success"] = "تم استبدال السؤال";

            return RedirectToAction(nameof(Preview), new { id = model.DraftId });
        }

        // ======================================================
        // Send Draft
        // ======================================================
        [HttpGet]
        public async Task<IActionResult> SendDraft(int draftId)
        {
            if (draftId <= 0)
            {
                TempData["Error"] = "يجب اختيار نموذج واجب قبل الإرسال.";
                return RedirectToAction(nameof(Index));
            }

            var instructorId = await RequireInstructorAsync();

            if (instructorId == 0)
                return Unauthorized();

            // تحميل المسودة لمعرفة الكورس
            var draft = await _context.HomeworkDrafts
                .AsNoTracking()
                .Where(d => d.Id == draftId)
                .Select(d => new { d.Id, d.CourseId })
                .FirstOrDefaultAsync();

            if (draft == null)
                return NotFound();

            var hasCourseAccess = await InstructorHasAccessToCourseAsync(instructorId, draft.CourseId);

            if (!hasCourseAccess)
                return Forbid();

            var allowedBatchIds = await GetInstructorAllowedBatchIdsAsync(instructorId);

            // جلب الدفعات الخاصة بالكورس
            var batchRows = await _context.Batches
                .AsNoTracking()
                .Where(b => !b.IsDeleted && b.CourseId == draft.CourseId)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                })
                .ToListAsync();

            var batches = batchRows
                .Where(b => int.TryParse(b.Value, out var batchId) && allowedBatchIds.Contains(batchId))
                .OrderBy(b => b.Text)
                .ToList();

            // جلب المحاضرات المرتبطة بهذا الكورس عبر الدفعات
            var lectureRows = await (
                from l in _context.Lecture.AsNoTracking()
                join b in _context.Batches.AsNoTracking()
                    on l.BatchId equals b.Id
                where !b.IsDeleted && b.CourseId == draft.CourseId
                orderby l.Date descending
                select new
                {
                    l.Id,
                    l.Title,
                    l.Date,
                    l.BatchId,
                    BatchName = b.Name
                }
            ).ToListAsync();

            var allowedLectureRows = lectureRows
                .Where(l => allowedBatchIds.Contains(l.BatchId))
                .ToList();

            var lectureItems = allowedLectureRows
                .Select(l => new SelectListItem
                {
                    Value = l.Id.ToString(),
                    Text = $"{l.Title} — {l.BatchName} ({l.Date:yyyy/MM/dd HH:mm})"
                })
                .ToList();

            var lectureDates = allowedLectureRows
                .ToDictionary(
                    l => l.Id,
                    l => l.Date.AddHours(1).ToString("yyyy-MM-ddTHH:mm")
                );

            var model = new SendHomeworkDraftVM
            {
                DraftId = draftId,
                StartAt = null,
                EndAt = DateTime.Now.AddDays(1),
                Batches = batches,
                Lectures = lectureItems,
                LectureDates = lectureDates
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendDraft(SendHomeworkDraftVM model)
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
                return Forbid();

            if (model.BatchIds == null || !model.BatchIds.Any())
            {
                ModelState.AddModelError(nameof(model.BatchIds), "يجب اختيار دفعة واحدة على الأقل.");
            }

            var draftCourseId = await _context.HomeworkDrafts
                .AsNoTracking()
                .Where(d => d.Id == model.DraftId)
                .Select(d => d.CourseId)
                .FirstOrDefaultAsync();

            if (draftCourseId <= 0)
            {
                return NotFound();
            }

            var hasCourseAccess = await InstructorHasAccessToCourseAsync(instructorId, draftCourseId);

            if (!hasCourseAccess)
                return Forbid();

            var allowedBatchIds = await GetInstructorAllowedBatchIdsAsync(instructorId);
            var selectedBatchIds = model.BatchIds ?? new List<int>();
            selectedBatchIds = selectedBatchIds.Distinct().ToList();
            model.BatchIds = selectedBatchIds;

            if (selectedBatchIds.Any(id => !allowedBatchIds.Contains(id)))
            {
                ModelState.AddModelError(nameof(model.BatchIds), "تم اختيار دفعة غير مرتبطة بصلاحيات المدرب.");
            }

            var draftCourseBatchIds = await _context.Batches
                .AsNoTracking()
                .Where(b =>
                    !b.IsDeleted &&
                    b.CourseId == draftCourseId)
                .Select(b => b.Id)
                .ToListAsync();

            if (selectedBatchIds.Any(id => !draftCourseBatchIds.Contains(id)))
            {
                ModelState.AddModelError(nameof(model.BatchIds), "تم اختيار دفعة لا تتبع دورة هذه المسودة.");
            }

            if (model.StartAt.HasValue && model.EndAt <= model.StartAt.Value)
            {
                ModelState.AddModelError(nameof(model.EndAt), "تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء.");
            }

            if (model.LectureId.HasValue)
            {
                var lectureBatchId = await _context.Lecture
                    .AsNoTracking()
                    .Where(l => l.Id == model.LectureId.Value)
                    .Select(l => (int?)l.BatchId)
                    .FirstOrDefaultAsync();

                if (!lectureBatchId.HasValue || !selectedBatchIds.Contains(lectureBatchId.Value))
                {
                    ModelState.AddModelError(nameof(model.LectureId), "المحاضرة المختارة غير مرتبطة بالدفعات المختارة.");
                }
            }

            var enrollmentRows = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Select(e => new
                {
                    e.BatchId,
                    e.StudentID,
                    e.Status
                })
                .ToListAsync();

            var selectedBatchStudentCount = enrollmentRows
                .Where(e =>
                    selectedBatchIds.Contains(e.BatchId) &&
                    string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.StudentID)
                .Distinct()
                .Count();

            if (selectedBatchStudentCount == 0)
            {
                ModelState.AddModelError(nameof(model.BatchIds), "لا يوجد طلاب نشطون في الدفعات المختارة.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateSendDraftOptionsAsync(model, draftCourseId, allowedBatchIds);
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                _assignmentService.SendDraftToBatches(
                    model,
                    model.DraftId,
                    userId
                );
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.GetBaseException().Message);
                await PopulateSendDraftOptionsAsync(model, draftCourseId, allowedBatchIds);
                return View(model);
            }

            TempData["Success"] = "تم إرسال الواجب";

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateSendDraftOptionsAsync(
            SendHomeworkDraftVM model,
            int courseId,
            List<int> allowedBatchIds)
        {
            var batchRows = await _context.Batches
                .AsNoTracking()
                .Where(b => !b.IsDeleted && b.CourseId == courseId)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                })
                .ToListAsync();

            model.Batches = batchRows
                .Where(b => int.TryParse(b.Value, out var batchId) && allowedBatchIds.Contains(batchId))
                .OrderBy(b => b.Text)
                .ToList();

            var lectureRows = await (
                from l in _context.Lecture.AsNoTracking()
                join b in _context.Batches.AsNoTracking()
                    on l.BatchId equals b.Id
                where !b.IsDeleted && b.CourseId == courseId
                orderby l.Date descending
                select new
                {
                    l.Id,
                    l.Title,
                    l.Date,
                    l.BatchId,
                    BatchName = b.Name
                }
            ).ToListAsync();

            var allowedLectureRows = lectureRows
                .Where(l => allowedBatchIds.Contains(l.BatchId))
                .ToList();

            model.Lectures = allowedLectureRows
                .Select(l => new SelectListItem
                {
                    Value = l.Id.ToString(),
                    Text = $"{l.Title} — {l.BatchName} ({l.Date:yyyy/MM/dd HH:mm})"
                })
                .ToList();

            model.LectureDates = allowedLectureRows
                .ToDictionary(
                    l => l.Id,
                    l => l.Date.AddHours(1).ToString("yyyy-MM-ddTHH:mm")
                );
        }

    }
}

