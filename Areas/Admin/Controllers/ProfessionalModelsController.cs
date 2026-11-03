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
using QdratNew.Security.AdminPermissions;
using QdratNew.Services.Interfaces;
using QdratNew.Services.ProfessionalModels;
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
        private readonly IProfessionalModelAvailabilityService _availabilityService;
        private readonly IAuthorizationService _authorization;

        public ProfessionalModelsController(
            ApplicationDbContext context,
            ITimeZoneService timeZoneService,
            IProfessionalModelAvailabilityService availabilityService,
            IAuthorizationService authorization)
        {
            _context = context;
            _timeZoneService = timeZoneService;
            _availabilityService = availabilityService;
            _authorization = authorization;
        }

        // =========================
        // PPM-S1: قسم «الإتاحة للشركاء» في Create/Edit/Visibility
        // =========================
        private async Task<bool> CanManageVisibilityAsync()
            => (await _authorization.AuthorizeAsync(User, AdminPermissionPolicies.ProfessionalModels_Visibility)).Succeeded;

        private async Task PopulatePartnerSectionAsync(IPartnerAudienceFields vm, bool canManage)
        {
            vm.CanManageVisibility = canManage;
            vm.PartnerOptions = canManage
                ? await _availabilityService.GetPartnerOptionsAsync(vm.SelectedPartnerIds)
                : new List<PartnerOptionVm>();
        }

        // الحالة المحفوظة للنموذج كما تُعرض في النموذج (Form)
        private async Task LoadPartnerAudienceAsync(IPartnerAudienceFields vm, PmEntity model, bool canManage)
        {
            if (canManage)
            {
                vm.IsForPartners = model.IsForPartners;
                vm.SelectedPartnerIds = await _availabilityService.GetAssignedPartnerIdsAsync(model.Id);

                var hasPeriods = await _context.ProfessionalModelSubscriptionPeriods
                    .AnyAsync(p => p.ProfessionalModelId == model.Id);

                vm.PartnerAudience = !model.IsGlobal && (vm.SelectedPartnerIds.Count > 0 || hasPeriods)
                    ? PartnerAudience.Selected
                    : PartnerAudience.All;
            }

            await PopulatePartnerSectionAsync(vm, canManage);
        }

        // يعيد الشركاء الموجودين فعلًا من الطلب، ويضيف خطأ ModelState عند جمهور محدد بلا أحد.
        // hasPeriods: النموذج مربوط بفترات اشتراك، وهي جمهور صالح بلا شركاء.
        private async Task<List<int>> ValidatePartnerAudienceAsync(IPartnerAudienceFields vm, bool hasPeriods, string emptyMessage)
        {
            if (!Enum.IsDefined(vm.PartnerAudience))
            {
                ModelState.AddModelError(nameof(vm.SelectedPartnerIds), "اختر جمهور الشركاء.");
                return new List<int>();
            }

            var partnerIds = await _availabilityService.FilterExistingPartnerIdsAsync(vm.SelectedPartnerIds);

            if (vm.IsForPartners && vm.PartnerAudience == PartnerAudience.Selected && partnerIds.Count == 0 && !hasPeriods)
                ModelState.AddModelError(nameof(vm.SelectedPartnerIds), emptyMessage);

            return partnerIds;
        }

        // 1. عرض جميع النماذج
        [AdminPermission("ProfessionalModels", "Read")]
        public async Task<IActionResult> Index(bool showArchived = false, int? curriculumId = null, ProfessionalModelType? modelType = null, bool untyped = false)
        {
            var query = _context.ProfessionalModels
                .Include(m => m.Curriculum)
                .Where(m => m.IsArchived == showArchived);

            if (curriculumId.HasValue && curriculumId.Value > 0)
                query = query.Where(m => m.CurriculumId == curriculumId.Value);

            // PPM-S2: القيمة 0 لا تُربط بـ Enum عبر الرابط، فللنماذج بلا نوع مُعامل مستقل
            if (untyped)
                query = query.Where(m => m.ModelType != ProfessionalModelType.Exam && m.ModelType != ProfessionalModelType.Homework);
            else if (modelType.HasValue)
                query = query.Where(m => m.ModelType == modelType.Value);

            var models = await query
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            ViewBag.ShowArchived = showArchived;
            ViewBag.ActiveModelsCount = await _context.ProfessionalModels.CountAsync(m => !m.IsArchived);
            ViewBag.ArchivedModelsCount = await _context.ProfessionalModels.CountAsync(m => m.IsArchived);
            ViewBag.SelectedCurriculumId = curriculumId;
            ViewBag.SelectedModelType = untyped ? null : modelType;
            ViewBag.UntypedOnly = untyped;
            ViewBag.UntypedModelsCount = await _context.ProfessionalModels.CountAsync(m =>
                !m.IsArchived && m.ModelType != ProfessionalModelType.Exam && m.ModelType != ProfessionalModelType.Homework);
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

            // PPM-S1.4: وسم الإتاحة لكل نموذج
            ViewBag.PartnerLinkCounts = await _context.ProfessionalModelPartners
                .AsNoTracking()
                .GroupBy(p => p.ProfessionalModelId)
                .Select(g => new { ModelId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ModelId, x => x.Count);

            ViewBag.PeriodLinkedModelIds = (await _context.ProfessionalModelSubscriptionPeriods
                .AsNoTracking()
                .Select(p => p.ProfessionalModelId)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            return View(models);
        }

        // PPM-S2: تصنيف جماعي للنماذج القديمة بلا نوع
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ProfessionalModels", "EditModel")]
        public async Task<IActionResult> ClassifyUntypedModels(List<int> selectedModelIds, ProfessionalModelType? modelType)
        {
            if (!modelType.HasValue || !Enum.IsDefined(modelType.Value))
            {
                TempData["ClassifyError"] = "نوع النموذج غير صحيح.";
                return RedirectToAction(nameof(Index), new { untyped = true });
            }

            if (selectedModelIds == null || selectedModelIds.Count == 0)
            {
                TempData["ClassifyError"] = "من فضلك اختر نموذجًا واحدًا على الأقل للتصنيف.";
                return RedirectToAction(nameof(Index), new { untyped = true });
            }

            var count = await _availabilityService.ClassifyUntypedModelsAsync(selectedModelIds, modelType.Value, HttpContext.RequestAborted);

            if (count == 0)
                TempData["ClassifyError"] = "لم يُصنَّف أي نموذج: النماذج المحددة مصنفة بالفعل أو مؤرشفة.";
            else
                TempData["ClassifySuccess"] = $"تم تصنيف {count} نموذج كـ{(modelType == ProfessionalModelType.Exam ? "اختبارات" : "واجبات")}.";

            return RedirectToAction(nameof(Index), new { untyped = true });
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
            await PopulatePartnerSectionAsync(vm, await CanManageVisibilityAsync());
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ProfessionalModels", "CreateModel")]
        public async Task<IActionResult> Create(ProfessionalModelCreateVm vm)
        {
            var canManage = await CanManageVisibilityAsync();
            var partnerIds = canManage
                ? await ValidatePartnerAudienceAsync(vm, hasPeriods: false, "اختر شريكًا واحدًا على الأقل.")
                : new List<int>();

            if (!ModelState.IsValid)
            {
                vm.Curriculums = await GetCurriculumsSelectList();
                await PopulatePartnerSectionAsync(vm, canManage);
                return View(vm);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            var modelId = await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync();

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

                if (canManage)
                    await _availabilityService.ApplyPartnerAudienceAsync(model, vm.IsForPartners, vm.PartnerAudience, partnerIds);

                await transaction.CommitAsync();
                return model.Id;
            });

            return RedirectToAction("Details", new { id = modelId });
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
        [ValidateAntiForgeryToken]
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
            await LoadPartnerAudienceAsync(vm, model, await CanManageVisibilityAsync());

            ViewBag.ModelId = id;
            return View(vm);
        }

        // 6. تعديل النموذج (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ProfessionalModels", "EditModel")]
        public async Task<IActionResult> Edit(int id, ProfessionalModelEditVm vm)
        {
            var canManage = await CanManageVisibilityAsync();
            var partnerIds = new List<int>();

            if (canManage)
            {
                var hasPeriods = await _context.ProfessionalModelSubscriptionPeriods
                    .AnyAsync(p => p.ProfessionalModelId == id);
                partnerIds = await ValidatePartnerAudienceAsync(vm, hasPeriods, "اختر شريكًا واحدًا على الأقل.");
            }

            // PPM-S2: النموذج بلا نوع لا يُحفظ حتى يُختار نوعه صراحة
            if (!Enum.IsDefined(vm.ModelType))
            {
                ModelState.Remove(nameof(vm.ModelType));
                ModelState.AddModelError(nameof(vm.ModelType), "اختر نوع النموذج: اختبارات أو واجبات.");
            }

            if (!ModelState.IsValid)
            {
                vm.Curriculums = await GetCurriculumsSelectList();
                await PopulatePartnerSectionAsync(vm, canManage);
                ViewBag.ModelId = id;
                return View(vm);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            var found = await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var model = await _context.ProfessionalModels.FindAsync(id);
                if (model == null) return false;

                model.Title = vm.Title;
                model.Description = vm.Description;
                model.CurriculumId = vm.CurriculumId;
                model.ModelType = vm.ModelType;

                _context.ProfessionalModels.Update(model);
                await _context.SaveChangesAsync();

                if (canManage)
                    await _availabilityService.ApplyPartnerAudienceAsync(model, vm.IsForPartners, vm.PartnerAudience, partnerIds);

                await transaction.CommitAsync();
                return true;
            });

            if (!found) return NotFound();

            TempData["SuccessMessage"] = "✅ تم تحديث النموذج بنجاح.";
            return RedirectToAction("Details", new { id });
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
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
                ModelType = original.ModelType,
                CurriculumId = original.CurriculumId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name,

                // 🔴 مهم جدًا: النسخة لا تكون Global ولا موجّهة للشركاء
                IsGlobal = false,
                IsForPartners = false
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
        public async Task<IActionResult> Visibility(int id)
        {
            var model = await _context.ProfessionalModels
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (model == null)
                return NotFound();

            var vm = new ProfessionalModelVisibilityVm
            {
                ProfessionalModelId = id,

                // فترات الاشتراك المحددة مسبقًا
                SelectedSubscriptionPeriodIds = await _context.ProfessionalModelSubscriptionPeriods
                    .Where(x => x.ProfessionalModelId == id)
                    .Select(x => x.PartnerSubscriptionPeriodId)
                    .ToListAsync(),

                SubscriptionPeriods = await GetSubscriptionPeriodsSelectList()
            };
            await LoadPartnerAudienceAsync(vm, model, canManage: true);

            return View(vm);
        }

        // قائمة فترات الاشتراك (بدون Title نهائيًا)
        private Task<List<SelectListItem>> GetSubscriptionPeriodsSelectList()
            => _context.PartnerSubscriptionPeriods
                .AsNoTracking()
                .Select(sp => new SelectListItem
                {
                    Value = sp.Id.ToString(),
                    Text =
                        sp.StartDate.ToString("yyyy-MM-dd")
                        + " → "
                        + sp.EndDate.ToString("yyyy-MM-dd")
                })
                .ToListAsync();



        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ProfessionalModels", "Visibility")]
        public async Task<IActionResult> Visibility(ProfessionalModelVisibilityVm vm)
        {
            if (!await _context.ProfessionalModels.AnyAsync(m => m.Id == vm.ProfessionalModelId))
                return NotFound();

            var partnerIds = await ValidatePartnerAudienceAsync(
                vm,
                hasPeriods: vm.SelectedSubscriptionPeriodIds.Count > 0,
                "اختر شريكًا واحدًا على الأقل أو فترة اشتراك.");

            if (!ModelState.IsValid)
            {
                vm.SubscriptionPeriods = await GetSubscriptionPeriodsSelectList();
                await PopulatePartnerSectionAsync(vm, canManage: true);
                return View(vm);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var model = await _context.ProfessionalModels.FindAsync(vm.ProfessionalModelId);
                await _availabilityService.ApplyPartnerAudienceAsync(model!, vm.IsForPartners, vm.PartnerAudience, partnerIds);

                // «داخلي» يبقي الروابط كلها محفوظة لإعادة التفعيل
                if (vm.IsForPartners)
                    await _availabilityService.SyncSubscriptionPeriodsAsync(vm.ProfessionalModelId, vm.SelectedSubscriptionPeriodIds);

                await transaction.CommitAsync();
            });

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
        [ValidateAntiForgeryToken]
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
