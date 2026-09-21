using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Question;

namespace QdratNew.Areas.Instructors.Controllers
{
    public class QuestionReviewController : BaseInstructorController
    {
        private readonly ApplicationDbContext _context;

        public QuestionReviewController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
            : base(userManager, scopeService)
        {
            _context = context;
        }

        // ─── GET: عرض أسئلة المؤشر ───────────────────────────────
        [HttpGet]
        public async Task<IActionResult> ByLesson(int lessonId)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            var lesson = await _context.Lessons
                .AsNoTracking()
                .Include(l => l.Section)
                    .ThenInclude(s => s.Curriculum)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null) return NotFound();

            var questions = await _context.Questions
                .AsNoTracking()
                .Where(q => q.LessonId == lessonId && !q.IsRejected)
                .OrderByDescending(q => q.IsReviewed)
                .ThenByDescending(q => q.CreatedAt)
                .Select(q => new QuestionReviewItem
                {
                    Id               = q.Id,
                    ReferenceNumber  = q.ReferenceNumber,
                    Title            = q.Title,
                    Difficulty       = q.Difficulty.ToString(),
                    IsReviewed       = q.IsReviewed,
                    IsComplete       = q.IsComplete,
                    HasCorrectAnswer = !string.IsNullOrWhiteSpace(q.CorrectAnswer),
                    OptionsCount     = q.Options.Count,
                    CreatedAt        = q.CreatedAt
                })
                .ToListAsync();

            ViewBag.LessonTitle     = lesson.Title;
            ViewBag.LessonId        = lessonId;
            ViewBag.SectionTitle    = lesson.Section?.Title;
            ViewBag.CurriculumTitle = lesson.Section?.Curriculum?.Title;
            ViewBag.TotalCount      = questions.Count;
            ViewBag.ReviewedCount   = questions.Count(q => q.IsReviewed);
            ViewBag.CompleteCount   = questions.Count(q => q.IsComplete);

            return View(questions);
        }

        // ─── AJAX: معاينة سؤال (يعيد Partial HTML) ──────────────
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

        // ─── GET: بنك الأسئلة — إضافة سؤال للمؤشر ───────────────
        [HttpGet]
        public async Task<IActionResult> BankBrowse(int lessonId)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            var lesson = await _context.Lessons
                .AsNoTracking()
                .Include(l => l.Section)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null) return NotFound();

            var questions = await _context.Questions
                .AsNoTracking()
                .Where(q => q.LessonId == lessonId && !q.IsRejected && q.IsReviewed && q.IsComplete)
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new QuestionReviewItem
                {
                    Id              = q.Id,
                    ReferenceNumber = q.ReferenceNumber,
                    Title           = q.Title,
                    Difficulty      = q.Difficulty.ToString(),
                    IsReviewed      = q.IsReviewed,
                    IsComplete      = q.IsComplete,
                    HasCorrectAnswer= !string.IsNullOrWhiteSpace(q.CorrectAnswer),
                    OptionsCount    = q.Options.Count,
                    CreatedAt       = q.CreatedAt
                })
                .ToListAsync();

            ViewBag.LessonTitle = lesson.Title;
            ViewBag.LessonId    = lessonId;
            ViewBag.SectionTitle= lesson.Section?.Title;

            return View(questions);
        }
    }

    public class QuestionReviewItem
    {
        public Guid     Id               { get; set; }
        public string   ReferenceNumber  { get; set; }
        public string   Title            { get; set; }
        public string   Difficulty       { get; set; }
        public bool     IsReviewed       { get; set; }
        public bool     IsComplete       { get; set; }
        public bool     HasCorrectAnswer { get; set; }
        public int      OptionsCount     { get; set; }
        public DateTime CreatedAt        { get; set; }
    }
}
