using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Services.Media;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class VerbalPassagesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPassageMediaService _mediaService;

        public VerbalPassagesController(ApplicationDbContext context, IPassageMediaService mediaService)
        {
            _context = context;
            _mediaService = mediaService;
        }


        [AdminPermission("VerbalPassages", "Read")]
        public async Task<IActionResult> Index()
        {
            var passages = await _context.VerbalPassages
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            return View(passages);
        }

        [HttpGet]
        [AdminPermission("VerbalPassages", "Create")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("VerbalPassages", "Create")]
        public async Task<IActionResult> Create(VerbalPassage model, IFormFile? mediaFile)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "⚠️ يرجى التأكد من إدخال جميع الحقول المطلوبة.";
                return View(model);
            }


            // =========================
            // 🔥 Validation للميديا
            // =========================
            if (model.Type != PassageType.Text && mediaFile == null && string.IsNullOrEmpty(model.MediaUrl))
            {
                TempData["Error"] = "❌ يجب رفع ملف صوت أو فيديو للقطعة.";
                return View(model);
            }



            // =========================
            // التحقق من الملف
            // =========================
            if (mediaFile != null)
            {
                if (!_mediaService.IsValidMedia(mediaFile))
                {
                    TempData["Error"] = "❌ الملف غير صالح. الصيغ المسموحة: mp3, mp4, wav وحجم أقل من 20MB.";
                    return View(model);
                }

                model.MediaUrl = await _mediaService.SaveMediaAsync(mediaFile);
            }

            model.CreatedAt = DateTime.Now;

            _context.VerbalPassages.Add(model);
            await _context.SaveChangesAsync();

            TempData["Message"] = "✅ تم إضافة القطعة بنجاح.";
            return RedirectToAction(nameof(Index));
        }


        [HttpGet]
        [AdminPermission("VerbalPassages", "Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var passage = await _context.VerbalPassages.FindAsync(id);
            if (passage == null) return NotFound();
            return View(passage);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("VerbalPassages", "Edit")]
        public async Task<IActionResult> Edit(int id, VerbalPassage model, IFormFile? mediaFile)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "⚠️ يرجى التأكد من إدخال جميع الحقول المطلوبة بشكل صحيح.";
                return View(model);
            }

            var passage = await _context.VerbalPassages.FindAsync(id);
            if (passage == null) return NotFound();

            // =========================
            // تحديث البيانات الأساسية
            // =========================
            passage.Title = model.Title;
            passage.Content = model.Content;
            passage.Type = model.Type;
            passage.DurationSeconds = model.DurationSeconds;
            passage.RequireFullListen = model.RequireFullListen;

            // =========================
            // تحديث الميديا
            // =========================
            if (mediaFile != null)
            {
                if (!_mediaService.IsValidMedia(mediaFile))
                {
                    TempData["Error"] = "❌ الملف غير صالح.";
                    return View(model);
                }

                // ❗ يمكن لاحقًا حذف القديم
                passage.MediaUrl = await _mediaService.SaveMediaAsync(mediaFile);
            }

            await _context.SaveChangesAsync();

            TempData["Message"] = "✅ تم تعديل القطعة بنجاح.";
            return RedirectToAction(nameof(Index));
        }


        [HttpGet]
        [AdminPermission("VerbalPassages", "Read")]
        public async Task<IActionResult> LinkedQuestions(int id)
        {
            var passage = await _context.VerbalPassages
                .FirstOrDefaultAsync(p => p.Id == id);

            if (passage == null)
                return NotFound();

            // =========================
            // تحديد اتجاه اللغة
            // =========================
            var firstQuestion = await _context.Questions
                .Where(q => q.VerbalPassageId == id)
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Section)
                        .ThenInclude(s => s.Curriculum)
                .Select(q => q.Lesson.Section.Curriculum.IsRTL)
                .FirstOrDefaultAsync();

            bool isRTL = firstQuestion;

            // =========================
            // إرسال البيانات للـ View
            // =========================
            ViewData["PassageTitle"] = passage.Title;
            ViewData["PassageContent"] = passage.Content;
            ViewData["PassageId"] = id;
            ViewData["IsRTL"] = isRTL;

            // 🔥 الجديد (مهم جدًا)
            ViewData["PassageType"] = (int)passage.Type;
            ViewData["PassageMediaUrl"] = passage.MediaUrl;
            ViewData["PassageDuration"] = passage.DurationSeconds;

            return View();
        }

        [HttpPost]
        [AdminPermission("VerbalPassages", "Read")]
        public async Task<IActionResult> LoadLinkedQuestionsData(int passageId)
        {
            var questions = await (
                from q in _context.Questions
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                join c in _context.Curriculums on s.CurriculumId equals c.Id
                where q.VerbalPassageId == passageId
                orderby q.CreatedAt descending
                select new
                {
                    q.Id,
                    q.Title,
                    Curriculum = c.Title,
                    Section = s.Title,
                    Lesson = l.Title,
                    q.CreatedAt,
                    q.IsAnswerConfirmed
                }
            ).ToListAsync();

            return Json(new { data = questions });
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("VerbalPassages", "Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var passage = await _context.VerbalPassages.FindAsync(id);
            if (passage == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على القطعة.";
                return RedirectToAction(nameof(Index));
            }

            _context.VerbalPassages.Remove(passage);
            await _context.SaveChangesAsync();

            TempData["Message"] = "✅ تم حذف القطعة بنجاح.";
            return RedirectToAction(nameof(Index));
        }
    }
}
