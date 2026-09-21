using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Extensions;
using QdratNew.Models;
using QdratNew.Security;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Course;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.ProfessionalModel;
using QdratNew.ViewModels.Question;
using CurriculumStatsVm = QdratNew.ViewModels.ProfessionalModel.CurriculumModelStatsVm;
using PmEntity = QdratNew.Entities.ProfessionalModel;
using QuestPDF.Drawing;
using QuestPDF.Elements;
using QuestPDF.Elements.Table;
using QuestPDF.Elements.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Previewer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    //[Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class ProfessionalModelsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITimeZoneService _timeZoneService;
        public ProfessionalModelsController(ApplicationDbContext context, ITimeZoneService timeZoneService)
        {
            _context = context;
            _timeZoneService = timeZoneService; 
        }

        // 1. عرض جميع النماذج
        [AdminPermission("ProfessionalModels", "Read")]
        public async Task<IActionResult> Index(bool showArchived = false, int? curriculumId = null, ProfessionalModelType? modelType = null)
        {
            var query = _context.ProfessionalModels
                .Include(m => m.Curriculum)
                .Where(m => m.IsArchived == showArchived);

            if (curriculumId.HasValue && curriculumId.Value > 0)
                query = query.Where(m => m.CurriculumId == curriculumId.Value);

            if (modelType.HasValue)
                query = query.Where(m => m.ModelType == modelType.Value);

            var models = await query
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            ViewBag.ShowArchived = showArchived;
            ViewBag.ActiveModelsCount = await _context.ProfessionalModels.CountAsync(m => !m.IsArchived);
            ViewBag.ArchivedModelsCount = await _context.ProfessionalModels.CountAsync(m => m.IsArchived);
            ViewBag.SelectedCurriculumId = curriculumId;
            ViewBag.SelectedModelType = modelType;
            ViewBag.Curriculums = await _context.Curriculums
                .AsNoTracking()
                .OrderBy(c => c.Title)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                .ToListAsync();

            // كروت المناهج — النماذج النشطة فقط المرتبطة بمنهج
            if (!showArchived)
            {
                var classifiedModels = await _context.ProfessionalModels
                    .Include(m => m.Curriculum)
                    .Where(m => !m.IsArchived && m.CurriculumId.HasValue)
                    .OrderBy(m => m.Title)
                    .ToListAsync();

                var curriculumStats = classifiedModels
                    .GroupBy(m => new { m.CurriculumId, m.Curriculum!.Title })
                    .Select(g => new CurriculumStatsVm
                    {
                        CurriculumId    = g.Key.CurriculumId!.Value,
                        CurriculumTitle = g.Key.Title,
                        HomeworkModels  = g.Where(m => m.ModelType == ProfessionalModelType.Homework).ToList(),
                        ExamModels      = g.Where(m => m.ModelType == ProfessionalModelType.Exam).ToList()
                    })
                    .Where(s => s.TotalCount > 0)
                    .OrderBy(s => s.CurriculumTitle)
                    .ToList();

                ViewBag.CurriculumStats = curriculumStats;
            }
            else
            {
                ViewBag.CurriculumStats = new List<CurriculumStatsVm>();
            }

            return View(models);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ProfessionalModels", "EditModel")]
        public async Task<IActionResult> ArchiveModelsByIds(List<int> selectedModelIds)
        {
            if (selectedModelIds == null || selectedModelIds.Count == 0)
            {
                TempData["Error"] = "من فضلك اختر نموذجًا واحدًا على الأقل للأرشفة.";
                return RedirectToAction(nameof(Index));
            }

            var selectedIds = selectedModelIds.ToHashSet();
            var models = (await _context.ProfessionalModels
                .Where(m => !m.IsArchived)
                .ToListAsync())
                .Where(m => selectedIds.Contains(m.Id))
                .ToList();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            foreach (var model in models)
            {
                model.IsArchived = true;
                model.ArchivedAt = DateTime.Now;
                model.ArchivedByUserId = userId;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم نقل {models.Count} نموذج إلى الأرشيف.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ProfessionalModels", "EditModel")]
        public async Task<IActionResult> RestoreModelsByIds(List<int> selectedModelIds)
        {
            if (selectedModelIds == null || selectedModelIds.Count == 0)
            {
                TempData["Error"] = "من فضلك اختر نموذجًا واحدًا على الأقل للاسترجاع.";
                return RedirectToAction(nameof(Index), new { showArchived = true });
            }

            var selectedIds = selectedModelIds.ToHashSet();
            var models = (await _context.ProfessionalModels
                .Where(m => m.IsArchived)
                .ToListAsync())
                .Where(m => selectedIds.Contains(m.Id))
                .ToList();

            foreach (var model in models)
            {
                model.IsArchived = false;
                model.ArchivedAt = null;
                model.ArchivedByUserId = null;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم إخراج {models.Count} نموذج من الأرشيف.";

            return RedirectToAction(nameof(Index), new { showArchived = true });
        }

        // 2. إنشاء نموذج جديد (GET)
        [HttpGet]
        [AdminPermission("ProfessionalModels", "CreateModel")]
        public async Task<IActionResult> Create()
        {
            var vm = new ProfessionalModelCreateVm
            {
                Curriculums = await GetCurriculumsSelectList()
            };
            return View(vm);
        }

        [HttpPost]
        [AdminPermission("ProfessionalModels", "CreateModel")]
        public async Task<IActionResult> Create(ProfessionalModelCreateVm vm)
        {
            if (!ModelState.IsValid)
            {
                vm.Curriculums = await GetCurriculumsSelectList();
                return View(vm);
            }

            // 🟢 إنشاء النموذج
            var model = new PmEntity
            {
                Title = vm.Title,
                Description = vm.Description,
                CurriculumId = vm.CurriculumId,
                ModelType = vm.ModelType,
                CreatedAt = _timeZoneService.GetNowUtc(),
                CreatedBy = User.Identity?.Name
            };

            _context.ProfessionalModels.Add(model);
            await _context.SaveChangesAsync();

            // 🟢 استيراد الأسئلة (لو في كود استيراد)
            if (!string.IsNullOrEmpty(vm.ImportFromCode))
            {
                var importCode = vm.ImportFromCode; // ✅ string ثابت
                var questions = await _context.Questions
                    .Where(q => q.InternalNote.StartsWith(importCode))
                    .ToListAsync();

                var ordered = questions.Select(q => new
                {
                    q.Id,
                    Order = ExtractOrderFromInternalNote(q.InternalNote)
                })
                .OrderBy(x => x.Order)
                .ToList();

                foreach (var q in ordered)
                {
                    _context.ProfessionalModelQuestions.Add(new ProfessionalModelQuestion
                    {
                        ModelId = model.Id,
                        QuestionId = q.Id,
                        OrderNumber = q.Order
                    });
                }
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Details", new { id = model.Id });
        }




        // =========================
        // Internal Filters For Professional Model Add Questions Page
        // هذه الأكشنات للعرض فقط داخل صفحة إضافة أسئلة النموذج
        // لا تعطي أي صلاحيات مستقلة على المناهج أو المحاور أو المؤشرات
        // =========================

        [HttpGet]
        [AdminPermission("ProfessionalModels", "EditModel")]
        public async Task<IActionResult> GetCurriculumsForQuestionFilters()
        {
            var data = await _context.Curriculums
                .AsNoTracking()
                .OrderBy(x => x.Title)
                .Select(x => new
                {
                    id = x.Id,
                    title = x.Title
                })
                .ToListAsync();

            return Json(data);
        }

        [HttpGet]
        [AdminPermission("ProfessionalModels", "EditModel")]
        public async Task<IActionResult> GetSectionsForQuestionFilters(int? curriculumId)
        {
            var query = _context.Sections
                .AsNoTracking()
                .AsQueryable();

            if (curriculumId.HasValue && curriculumId.Value > 0)
            {
                query = query.Where(x => x.CurriculumId == curriculumId.Value);
            }

            var data = await query
                .OrderBy(x => x.Title)
                .Select(x => new
                {
                    id = x.Id,
                    title = x.Title
                })
                .ToListAsync();

            return Json(data);
        }

        [HttpGet]
        [AdminPermission("ProfessionalModels", "EditModel")]
        public async Task<IActionResult> GetLessonsForQuestionFilters(int sectionId)
        {
            if (sectionId <= 0)
                return Json(new List<object>());

            var data = await _context.Lessons
                .AsNoTracking()
                .Where(x =>
                    x.SectionId == sectionId &&
                    x.IsActive)
                .OrderBy(x => x.Title)
                .Select(x => new
                {
                    id = x.Id,
                    title = x.Title
                })
                .ToListAsync();

            return Json(data);
        }




        // 3. تفاصيل النموذج + الأسئلة
        // 3. تفاصيل النموذج + الأسئلة
        [AdminPermission("ProfessionalModels", "Read")]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _context.ProfessionalModels
                .Include(m => m.Questions)
                    .ThenInclude(mq => mq.Question)
                        .ThenInclude(q => q.Options) // ✅ تحميل الاختيارات
                .Include(m => m.Questions)
                    .ThenInclude(mq => mq.Question)
                        .ThenInclude(q => q.Lesson)
                            .ThenInclude(l => l.Section) // ✅ تحميل المؤشر والمحور
                .Include(m => m.Questions)
                    .ThenInclude(mq => mq.Question)
                        .ThenInclude(q => q.VerbalPassage) // ✅ تحميل القطعة اللفظية
                .FirstOrDefaultAsync(m => m.Id == id);

            if (model == null)
                return NotFound();

            return View(model);
        }
     
        
        
        
        
        [HttpGet]
        [AdminPermission("ProfessionalModels", "Read")]
        public async Task<IActionResult> PrintContinuous(int id)
        {
            var model = await _context.ProfessionalModels
                .Include(m => m.Questions)
                    .ThenInclude(mq => mq.Question)
                        .ThenInclude(q => q.Options)

                .Include(m => m.Questions)
                    .ThenInclude(mq => mq.Question)
                        .ThenInclude(q => q.Lesson)
                            .ThenInclude(l => l.Section)
                                .ThenInclude(s => s.Curriculum) // ✅ أهم إضافة

                .Include(m => m.Questions)
                    .ThenInclude(mq => mq.Question)
                        .ThenInclude(q => q.VerbalPassage)

                .FirstOrDefaultAsync(m => m.Id == id);

            if (model == null)
                return NotFound();

            // =====================================================
            // ✅ تحديد الاتجاه بناء على أول سؤال (كافي لأنهم نفس المنهج)
            // =====================================================
            var isRTL = model.Questions
                .Select(q => q.Question?.Lesson?.Section?.Curriculum?.IsRTL)
                .FirstOrDefault() ?? true;

            // =====================================================
            // ✅ تمرير الاتجاه للـ View
            // =====================================================
            ViewBag.IsRTL = isRTL;

            return View(model);
        }



        [HttpGet]
        [AdminPermission("ProfessionalModels", "Read")]
        public async Task<IActionResult> PrintView(int id)
        {
            var model = await _context.ProfessionalModels
                .Include(m => m.Questions)
                    .ThenInclude(mq => mq.Question)
                        .ThenInclude(q => q.Options)

                .Include(m => m.Questions)
                    .ThenInclude(mq => mq.Question)
                        .ThenInclude(q => q.Lesson)
                            .ThenInclude(l => l.Section)
                                .ThenInclude(s => s.Curriculum) // ✅ مهم

                .Include(m => m.Questions)
                    .ThenInclude(mq => mq.Question)
                        .ThenInclude(q => q.VerbalPassage)

                .FirstOrDefaultAsync(m => m.Id == id);

            if (model == null)
                return NotFound();

            var firstCurriculum = model.Questions
      .Select(q => q.Question?.Lesson?.Section?.Curriculum)
      .FirstOrDefault(c => c != null);

            var isRTL = firstCurriculum?.IsRTL ?? true;

            ViewBag.IsRTL = isRTL;

            return View(model);
        }


        // 4. عرض صفحة إضافة أسئلة يدويًا
        [HttpGet]
        [AdminPermission("ProfessionalModels", "EditModel")]
        public async Task<IActionResult> AddQuestions(int id)
        {
            var model = await _context.ProfessionalModels.FindAsync(id);
            if (model == null) return NotFound();

            var vm = new ProfessionalModelAddQuestionsVm
            {
                ModelId = model.Id,
                ModelTitle = model.Title
            };

            return View(vm);
        }



        [HttpPost]
        [AdminPermission("ProfessionalModels", "EditModel")]
        public async Task<IActionResult> AddQuestions(int modelId, List<Guid> questionIds)
        {
            if (questionIds == null || !questionIds.Any())
            {
                TempData["ErrorMessage"] = "يجب اختيار سؤال واحد على الأقل.";
                return RedirectToAction(nameof(AddQuestions), new { id = modelId });
            }

            foreach (var qId in questionIds)
            {
                bool exists = await _context.ProfessionalModelQuestions
                    .AnyAsync(x => x.ModelId == modelId && x.QuestionId == qId);

                if (!exists)
                {
                    _context.ProfessionalModelQuestions.Add(new ProfessionalModelQuestion
                    {
                        ModelId = modelId,
                        QuestionId = qId,
                        OrderNumber = 999
                    });
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = modelId });
        }






        // 8. حذف النموذج (GET) - صفحة تأكيد
        [HttpGet]
        [AdminPermission("ProfessionalModels", "DeleteModel")]
        public async Task<IActionResult> Delete(int id)
        {
            var model = await _context.ProfessionalModels.FindAsync(id);
            if (model == null) return NotFound();

            return View(model); // يفتح صفحة Delete.cshtml
        }

        // 8. حذف النموذج (POST) - تنفيذ الحذف
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AdminPermission("ProfessionalModels", "DeleteModel")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var model = await _context.ProfessionalModels
                .Include(m => m.Questions) // حذف العلاقات أولاً
                .FirstOrDefaultAsync(m => m.Id == id);

            if (model == null) return NotFound();

            // 🟠 نحذف الأسئلة المرتبطة من الجدول الوسيط
            if (model.Questions != null && model.Questions.Any())
                _context.ProfessionalModelQuestions.RemoveRange(model.Questions);

            _context.ProfessionalModels.Remove(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "🗑️ تم حذف النموذج بنجاح.";
            return RedirectToAction(nameof(Index));
        }






        // 6. تعديل النموذج (GET)
        [HttpGet]
        [AdminPermission("ProfessionalModels", "EditModel")]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _context.ProfessionalModels.FindAsync(id);
            if (model == null) return NotFound();

            var vm = new ProfessionalModelEditVm
            {
                Title = model.Title,
                Description = model.Description,
                CurriculumId = model.CurriculumId,
                ModelType = model.ModelType,
                Curriculums = await GetCurriculumsSelectList()
            };

            ViewBag.ModelId = id;
            return View(vm);
        }

        // 6. تعديل النموذج (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ProfessionalModels", "EditModel")]
        public async Task<IActionResult> Edit(int id, ProfessionalModelEditVm vm)
        {
            if (!ModelState.IsValid)
            {
                vm.Curriculums = await GetCurriculumsSelectList();
                ViewBag.ModelId = id;
                return View(vm);
            }

            var model = await _context.ProfessionalModels.FindAsync(id);
            if (model == null) return NotFound();

            model.Title = vm.Title;
            model.Description = vm.Description;
            model.CurriculumId = vm.CurriculumId;
            model.ModelType = vm.ModelType;

            _context.ProfessionalModels.Update(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ تم تحديث النموذج بنجاح.";
            return RedirectToAction("Details", new { id });
        }



        [HttpPost]
        [AdminPermission("ProfessionalModels", "Duplicate")]
        public async Task<IActionResult> Duplicate(int id)
        {
            var original = await _context.ProfessionalModels
                .Include(m => m.Questions)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (original == null)
                return NotFound();

            // 🟢 إنشاء نسخة جديدة من النموذج
            var duplicated = new PmEntity
            {
                Title = original.Title + " - نسخة",
                Description = original.Description,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name,

                // 🔴 مهم جدًا: النسخة لا تكون Global
                IsGlobal = false
            };

            _context.ProfessionalModels.Add(duplicated);
            await _context.SaveChangesAsync();

            // 🟢 نسخ الأسئلة المرتبطة فقط
            if (original.Questions != null && original.Questions.Any())
            {
                foreach (var q in original.Questions)
                {
                    _context.ProfessionalModelQuestions.Add(new ProfessionalModelQuestion
                    {
                        ModelId = duplicated.Id,
                        QuestionId = q.QuestionId,
                        OrderNumber = q.OrderNumber
                    });
                }

                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "✅ تم إنشاء نسخة جديدة من النموذج بنجاح. يرجى تحديد صلاحيات الظهور.";
            return RedirectToAction("Visibility", new { id = duplicated.Id });
        }


        [HttpGet]
        [AdminPermission("ProfessionalModels", "Visibility")]
        public IActionResult Visibility(int id)
        {
            var model = _context.ProfessionalModels
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == id);

            if (model == null)
                return NotFound();

            var vm = new ProfessionalModelVisibilityVm
            {
                ProfessionalModelId = id,
                IsGlobal = model.IsGlobal,

                // الشركاء المحددون مسبقًا
                SelectedPartnerIds = _context.ProfessionalModelPartners
                    .Where(x => x.ProfessionalModelId == id)
                    .Select(x => x.PartnerId)
                    .ToList(),

                // فترات الاشتراك المحددة مسبقًا
                SelectedSubscriptionPeriodIds = _context.ProfessionalModelSubscriptionPeriods
                    .Where(x => x.ProfessionalModelId == id)
                    .Select(x => x.PartnerSubscriptionPeriodId)
                    .ToList(),

                // قائمة الشركاء
                Partners = _context.Partners
                    .AsNoTracking()
                    .Select(p => new SelectListItem
                    {
                        Value = p.Id.ToString(),
                        Text = p.Name
                    })
                    .ToList(),

                // قائمة فترات الاشتراك (بدون Title نهائيًا)
                SubscriptionPeriods = _context.PartnerSubscriptionPeriods
    .AsNoTracking()
    .Select(sp => new SelectListItem
    {
        Value = sp.Id.ToString(),
        Text =
            sp.StartDate.ToString("yyyy-MM-dd")
            + " → "
            + sp.EndDate.ToString("yyyy-MM-dd")
    })
    .ToList()

            };

            return View(vm);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ProfessionalModels", "Visibility")]
        public IActionResult Visibility(ProfessionalModelVisibilityVm vm)
        {
            var model = _context.ProfessionalModels.Find(vm.ProfessionalModelId);
            if (model == null) return NotFound();

            // 1️⃣ تحديث Global
            model.IsGlobal = vm.IsGlobal;

            // 2️⃣ تنظيف التخصيصات القديمة
            _context.ProfessionalModelPartners
                .RemoveRange(_context.ProfessionalModelPartners
                    .Where(x => x.ProfessionalModelId == vm.ProfessionalModelId));

            _context.ProfessionalModelSubscriptionPeriods
                .RemoveRange(_context.ProfessionalModelSubscriptionPeriods
                    .Where(x => x.ProfessionalModelId == vm.ProfessionalModelId));

            // 3️⃣ إضافة الشركاء
            foreach (var partnerId in vm.SelectedPartnerIds)
            {
                _context.ProfessionalModelPartners.Add(new Entities.ProfessionalModelPartner
                {
                    ProfessionalModelId = vm.ProfessionalModelId,
                    PartnerId = partnerId
                });
            }

            // 4️⃣ إضافة الفترات
            foreach (var periodId in vm.SelectedSubscriptionPeriodIds)
            {
                _context.ProfessionalModelSubscriptionPeriods.Add(new Entities.ProfessionalModelSubscriptionPeriod
                {
                    ProfessionalModelId = vm.ProfessionalModelId,
                    PartnerSubscriptionPeriodId = periodId
                });
            }

            _context.SaveChanges();

            TempData["SuccessMessage"] = "تم تحديث صلاحيات ظهور النموذج بنجاح.";
            return RedirectToAction("Details", new { id = vm.ProfessionalModelId });
        }


        // =========================
        // 5) API – تحميل الأسئلة (Server-Side) مع الفلاتر
        // =========================
        [HttpPost]
        [AdminPermission("ProfessionalModels", "EditModel")]
        public async Task<IActionResult> LoadQuestionsData(
       int modelId,
       int? curriculumId,
       int? sectionId,
       int? lessonId)
        {
            var draw = Request.Form["draw"].FirstOrDefault();

            var startText = Request.Form["start"].FirstOrDefault();
            var lengthText = Request.Form["length"].FirstOrDefault();

            var start = int.TryParse(startText, out var parsedStart) ? parsedStart : 0;
            var length = int.TryParse(lengthText, out var parsedLength) ? parsedLength : 25;

            if (start < 0)
                start = 0;

            if (length <= 0)
                length = 25;

            var searchValue = Request.Form["search[value]"].FirstOrDefault();

            var titleSearch = Request.Form["titleSearch"].FirstOrDefault();
            var curriculumSearch = Request.Form["curriculumSearch"].FirstOrDefault();
            var sectionSearch = Request.Form["sectionSearch"].FirstOrDefault();
            var lessonSearch = Request.Form["lessonSearch"].FirstOrDefault();
            var passageSearch = Request.Form["passageSearch"].FirstOrDefault();
            var internalNoteSearch = Request.Form["internalNoteSearch"].FirstOrDefault();

            var query = _context.Questions
                .AsNoTracking()
                .Where(q =>
                    q.IsReviewed &&
                    q.IsComplete &&
                    !_context.ProfessionalModelQuestions
                        .Any(pm => pm.ModelId == modelId && pm.QuestionId == q.Id));

            if (curriculumId.HasValue && curriculumId.Value > 0)
            {
                query = query.Where(q => q.CurriculumId == curriculumId.Value);
            }

            if (sectionId.HasValue && sectionId.Value > 0)
            {
                query = query.Where(q => q.Lesson.SectionId == sectionId.Value);
            }

            if (lessonId.HasValue && lessonId.Value > 0)
            {
                query = query.Where(q => q.LessonId == lessonId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                var pattern = "%" + searchValue.Trim() + "%";

                query = query.Where(q =>
                    (q.Title != null && EF.Functions.Like(q.Title, pattern)) ||
                    (q.InternalNote != null && EF.Functions.Like(q.InternalNote, pattern)) ||
                    (q.Curriculum.Title != null && EF.Functions.Like(q.Curriculum.Title, pattern)) ||
                    (q.Lesson.Section.Title != null && EF.Functions.Like(q.Lesson.Section.Title, pattern)) ||
                    (q.Lesson.Title != null && EF.Functions.Like(q.Lesson.Title, pattern)) ||
                    (q.VerbalPassage != null && q.VerbalPassage.Title != null && EF.Functions.Like(q.VerbalPassage.Title, pattern)));
            }

            if (!string.IsNullOrWhiteSpace(titleSearch))
            {
                var pattern = "%" + titleSearch.Trim() + "%";

                query = query.Where(q =>
                    q.Title != null &&
                    EF.Functions.Like(q.Title, pattern));
            }

            if (!string.IsNullOrWhiteSpace(curriculumSearch))
            {
                var pattern = "%" + curriculumSearch.Trim() + "%";

                query = query.Where(q =>
                    q.Curriculum.Title != null &&
                    EF.Functions.Like(q.Curriculum.Title, pattern));
            }

            if (!string.IsNullOrWhiteSpace(sectionSearch))
            {
                var pattern = "%" + sectionSearch.Trim() + "%";

                query = query.Where(q =>
                    q.Lesson.Section.Title != null &&
                    EF.Functions.Like(q.Lesson.Section.Title, pattern));
            }

            if (!string.IsNullOrWhiteSpace(lessonSearch))
            {
                var pattern = "%" + lessonSearch.Trim() + "%";

                query = query.Where(q =>
                    q.Lesson.Title != null &&
                    EF.Functions.Like(q.Lesson.Title, pattern));
            }

            if (!string.IsNullOrWhiteSpace(passageSearch))
            {
                var pattern = "%" + passageSearch.Trim() + "%";

                query = query.Where(q =>
                    q.VerbalPassage != null &&
                    q.VerbalPassage.Title != null &&
                    EF.Functions.Like(q.VerbalPassage.Title, pattern));
            }

            if (!string.IsNullOrWhiteSpace(internalNoteSearch))
            {
                var pattern = "%" + internalNoteSearch.Trim() + "%";

                query = query.Where(q =>
                    q.InternalNote != null &&
                    EF.Functions.Like(q.InternalNote, pattern));
            }

            var recordsTotal = await query.CountAsync();

            var pageItems = await query
                .OrderByDescending(q => q.CreatedAt)
                .Skip(start)
                .Take(length)
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    CurriculumTitle = q.Curriculum.Title,
                    SectionTitle = q.Lesson.Section.Title,
                    LessonTitle = q.Lesson.Title,
                    q.InternalNote,
                    PassageTitle = q.VerbalPassage != null ? q.VerbalPassage.Title : null,
                    VerbalPassage = q.VerbalPassage
                })
                .ToListAsync();

            var data = pageItems.Select(q =>
            {
                var mediaType = ResolvePassageMediaType(q.VerbalPassage);

                return new
                {
                    q.Id,
                    Title = q.Title,
                    q.CurriculumTitle,
                    q.SectionTitle,
                    q.LessonTitle,
                    q.InternalNote,
                    PassageTitle = q.PassageTitle,
                    PassageMediaType = mediaType,
                    PassageMediaLabel = ResolvePassageMediaLabel(mediaType)
                };
            }).ToList();

            return Json(new
            {
                draw,
                recordsTotal,
                recordsFiltered = recordsTotal,
                data
            });
        }


        private string? GetStringPropertyValue(object? source, params string[] propertyNames)
        {
            if (source == null || propertyNames == null || propertyNames.Length == 0)
                return null;

            var sourceType = source.GetType();

            foreach (var propertyName in propertyNames)
            {
                var property = sourceType.GetProperty(propertyName);

                if (property == null)
                    continue;

                var value = property.GetValue(source, null);

                if (value == null)
                    continue;

                var stringValue = value.ToString();

                if (!string.IsNullOrWhiteSpace(stringValue))
                    return stringValue;
            }

            return null;
        }

        private string? GetPassageAudioUrl(object? passage)
        {
            return GetStringPropertyValue(
                passage,
                "AudioUrl",
                "AudioFileUrl",
                "AudioPath",
                "AudioFilePath",
                "MediaUrl",
                "FileUrl",
                "VoiceUrl"
            );
        }

        private string? GetPassageContent(object? passage)
        {
            return GetStringPropertyValue(
                passage,
                "Content",
                "Text",
                "Body"
            );
        }

        private string ResolvePassageMediaType(object? passage)
        {
            if (passage == null)
                return "none";

            var audioUrl = GetPassageAudioUrl(passage);

            if (!string.IsNullOrWhiteSpace(audioUrl))
                return "audio";

            var content = GetPassageContent(passage);

            if (!string.IsNullOrWhiteSpace(content))
                return "text";

            return "unknown";
        }

        private string ResolvePassageMediaLabel(string mediaType)
        {
            return mediaType switch
            {
                "audio" => "وسائط صوتية",
                "text" => "نص",
                "unknown" => "غير محدد",
                _ => "—"
            };
        }




        [HttpGet]
        [AdminPermission("PlacementExams", "Read")]
        public async Task<IActionResult> Review(int examId)
        {
            var exam = await _context.Exams
                .Include(e => e.Questions)
                    .ThenInclude(eq => eq.Question)
                        .ThenInclude(q => q.Lesson)
                            .ThenInclude(l => l.Section)
                                .ThenInclude(s => s.Curriculum)
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null)
                return NotFound();

            var batch = await _context.ExamAssignmentsToBatches
                .Where(x => x.ExamId == examId)
                .Select(x => x.Batch.Name)
                .FirstOrDefaultAsync();

            var model = new PlacementExamReviewVm
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                CurriculumTitle = exam.Questions
                    .Select(q => q.Question.Lesson.Section.Curriculum.Title)
                    .FirstOrDefault(),

                BatchName = batch,
                TotalQuestions = exam.Questions.Count,
                DurationMinutes = exam.DurationMinutes,
                CreatedAt = exam.CreatedAt,

                Sections = exam.Questions
                    .GroupBy(q => q.Question.Lesson.Section)
                    .Select(g => new PlacementExamReviewSectionVm
                    {
                        SectionId = g.Key.Id,
                        SectionTitle = g.Key.Title,
                        Questions = g.Select(x => new PlacementExamReviewQuestionVm
                        {
                            QuestionId = x.QuestionId,
                            Title = x.Question.Title,
                            LessonTitle = x.Question.Lesson.Title,
                            Difficulty = x.Question.Difficulty,
                            CorrectAnswer = x.Question.CorrectAnswer,
                            Order = x.Order
                        }).OrderBy(q => q.Order).ToList()
                    }).ToList()
            };

            return View(model);
        }

        // =========================
        // 6) إضافة الأسئلة المختارة
        // =========================



        // 5. حذف سؤال من النموذج
        [HttpPost]
        [AdminPermission("ProfessionalModels", "EditModel")]
        public async Task<IActionResult> RemoveQuestion(int id, int modelId)
        {
            var item = await _context.ProfessionalModelQuestions.FindAsync(id);
            if (item != null)
            {
                _context.ProfessionalModelQuestions.Remove(item);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Details", new { id = modelId });
        }

        // Helper لاستخراج الرقم بعد Q
        private int ExtractOrderFromInternalNote(string note)
        {
            if (string.IsNullOrEmpty(note)) return 0;
            var idx = note.IndexOf("Q");
            if (idx == -1) return 0;
            var num = note.Substring(idx + 1);
            return int.TryParse(num, out int order) ? order : 0;
        }

        private async Task<List<SelectListItem>> GetCurriculumsSelectList()
        {
            var items = await _context.Curriculums
                .AsNoTracking()
                .OrderBy(c => c.Title)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                .ToListAsync();

            items.Insert(0, new SelectListItem { Value = "", Text = "— اختر المنهج (اختياري) —" });
            return items;
        }
    }
}
