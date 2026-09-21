using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Helpers;
using QdratNew.ViewModels.Remedial;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class RemedialVideosController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public RemedialVideosController(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            using var _context = _contextFactory.CreateDbContext();
            var videos = await _context.RemedialVideos
                .Include(v => v.Lesson).ThenInclude(l => l.Section)
                .OrderByDescending(v => v.Id)
                .ToListAsync();
            return View(videos);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            using var _context = _contextFactory.CreateDbContext();
            var curriculums = await _context.Curriculums
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            ViewBag.Curriculums = curriculums;
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RemedialVideoCreateVm model)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🧩 في حال لم يتم اختيار المؤشر
            if (model.LessonId <= 0)
            {
                ModelState.AddModelError("LessonId", "⚠️ يجب اختيار المؤشر (الدرس) المرتبط بالفيديو.");
            }

            if (!ModelState.IsValid)
            {
                // 🔁 إعادة تحميل المناهج في حال إعادة عرض الصفحة
                var curriculums = await _context.Curriculums
                    .Select(c => new { c.Id, c.Title })
                    .ToListAsync();
                ViewBag.Curriculums = curriculums;

                TempData["Error"] = "⚠️ الرجاء مراجعة البيانات قبل الحفظ.";
                return View(model);
            }

            // ✅ إنشاء الفيديو
            var video = new RemedialVideo
            {
                LessonId = model.LessonId,
                Title = model.Title?.Trim(),
                VimeoUrl = model.VimeoUrl?.Trim(),
                Description = model.Description,
                IsActive = true
            };
            video.ThumbnailUrl = await VideoThumbnailHelper.GetThumbnailAsync(video.VimeoUrl);

            _context.RemedialVideos.Add(video);
            await _context.SaveChangesAsync();

            // 🔗 ربط الأسئلة إذا تم تحديدها
            if (model.SelectedQuestionIds != null && model.SelectedQuestionIds.Any())
            {
                foreach (var qid in model.SelectedQuestionIds)
                {
                    _context.RemedialVideoQuestions.Add(new RemedialVideoQuestion
                    {
                        RemedialVideoId = video.Id,
                        QuestionId = qid
                    });
                }

                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "✅ تم إضافة الفيديو بنجاح وربطه بالأسئلة المختارة.";
            return RedirectToAction("Index");
        }


        // ✅ جلب المحاور داخل منهج محدد
        [HttpGet]
        public async Task<IActionResult> GetSectionsByCurriculum(int curriculumId)
        {
            using var _context = _contextFactory.CreateDbContext();
            var sections = await _context.Sections
                .Where(s => s.CurriculumId == curriculumId)
                .Select(s => new { s.Id, s.Title })
                .ToListAsync();
            return Json(sections);
        }

        // ✅ جلب المؤشرات الفعّالة داخل محور محدد
        [HttpGet]
        public async Task<IActionResult> GetLessonsBySection(int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();
            var lessons = await _context.Lessons
                .Where(l => l.SectionId == sectionId && l.IsActive == true)
                .Select(l => new { l.Id, l.Title })
                .ToListAsync();
            return Json(lessons);
        }






        // ✅ جلب الأسئلة الخاصة بالمؤشر المحدد
        // ✅ جلب الأسئلة الخاصة بالمؤشر المحدد
        [HttpGet]
        public async Task<IActionResult> GetQuestionsByLesson(int lessonId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var questions = await (
                from q in _context.Questions
                join l in _context.Lessons on q.LessonId equals l.Id
                where q.LessonId == lessonId
                select new
                {
                    id = q.Id,
                    text = q.Title, // ← هذا هو الحقل الصحيح
                    lessonTitle = l.Title
                }).ToListAsync();

            return Json(questions);
        }


        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var video = await _context.RemedialVideos
                .Include(v => v.Lesson)
                    .ThenInclude(l => l.Section)
                        .ThenInclude(s => s.Curriculum)
                .Include(v => v.Questions)
                    .ThenInclude(rvq => rvq.Question)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (video == null)
                return NotFound("❌ لم يتم العثور على الفيديو.");

            var vm = new QdratNew.ViewModels.Remedial.RemedialVideoDetailsVm
            {
                Id = video.Id,
                Title = video.Title,
                VimeoUrl = video.VimeoUrl,
                Description = video.Description,
                IsActive = video.IsActive,
                LessonTitle = video.Lesson?.Title,
                SectionTitle = video.Lesson?.Section?.Title,
                CurriculumTitle = video.Lesson?.Section?.Curriculum?.Title,
                Questions = video.Questions
                    .Select(q => new QdratNew.ViewModels.Remedial.RemedialVideoQuestionVm
                    {
                        QuestionId = q.QuestionId,
                        QuestionTitle = q.Question.Title
                    }).ToList()
            };

            return View(vm);
        }



        // ✅ عرض صفحة التعديل
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var video = await _context.RemedialVideos
                .Include(v => v.Lesson)
                    .ThenInclude(l => l.Section)
                        .ThenInclude(s => s.Curriculum)
                .Include(v => v.Questions) // ← مهم جدًا
                .ThenInclude(rvq => rvq.Question)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (video == null)
                return NotFound();

            // ✅ تجميع المعرفات للأسئلة المرتبطة بهذا الفيديو
            var selectedQuestionIds = await _context.RemedialVideoQuestions
                .Where(x => x.RemedialVideoId == video.Id)
                .Select(x => x.QuestionId)
                .ToListAsync();

            var model = new RemedialVideoEditVm
            {
                Id = video.Id,
                Title = video.Title,
                VimeoUrl = video.VimeoUrl,
                Description = video.Description,
                LessonId = video.LessonId,
                SectionId = video.Lesson?.SectionId,
                CurriculumId = video.Lesson?.Section?.CurriculumId,
                IsActive = video.IsActive,
                SelectedQuestionIds = selectedQuestionIds // ✅ أهم خطوة
            };

            // ✅ تحميل القوائم للـ dropdowns
            ViewBag.Curriculums = await _context.Curriculums
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            ViewBag.Sections = await _context.Sections
                .Select(s => new { s.Id, s.Title, s.CurriculumId })
                .ToListAsync();

            ViewBag.Lessons = await _context.Lessons
                .Select(l => new { l.Id, l.Title, l.SectionId })
                .ToListAsync();

            return View(model);
        }



        // ✅ حفظ التعديلات
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, QdratNew.ViewModels.Remedial.RemedialVideoCreateVm model)
        {
            using var _context = _contextFactory.CreateDbContext();

            var video = await _context.RemedialVideos
                .Include(v => v.Questions)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (video == null)
                return NotFound("❌ لم يتم العثور على الفيديو.");

            if (!ModelState.IsValid)
            {
                var curriculums = await _context.Curriculums
                    .Select(c => new { c.Id, c.Title })
                    .ToListAsync();
                ViewBag.Curriculums = curriculums;
                TempData["Error"] = "⚠️ تحقق من صحة البيانات المدخلة.";
                return View(model);
            }

            // ✅ تحديث بيانات الفيديو
            video.Title = model.Title;
            video.VimeoUrl = model.VimeoUrl;
            video.Description = model.Description;

            // ✅ تحديث الأسئلة المرتبطة
            var existingQuestions = video.Questions.ToList();
            _context.RemedialVideoQuestions.RemoveRange(existingQuestions);

            if (model.SelectedQuestionIds != null && model.SelectedQuestionIds.Any())
            {
                foreach (var qid in model.SelectedQuestionIds)
                {
                    _context.RemedialVideoQuestions.Add(new RemedialVideoQuestion
                    {
                        RemedialVideoId = video.Id,
                        QuestionId = qid
                    });
                }
            }
            video.ThumbnailUrl = await VideoThumbnailHelper.GetThumbnailAsync(video.VimeoUrl);

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم تحديث الفيديو والأسئلة المرتبطة به بنجاح.";
            return RedirectToAction("Details", new { id = video.Id });
        }






        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            using var _context = _contextFactory.CreateDbContext();
            var video = await _context.RemedialVideos.FindAsync(id);
            if (video == null)
                return Json(new { success = false, message = "لم يتم العثور على الفيديو." });

            video.IsActive = !video.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = video.IsActive ? "✅ تم تفعيل الفيديو." : "⚠️ تم إيقاف الفيديو." });
        }
    }
}
