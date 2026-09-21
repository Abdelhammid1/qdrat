using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services.Exams.Generators;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using static QdratNew.ViewModels.Exam.ManualExamCreationViewModel;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ExamGeneratorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurriculumExamGeneratorService _curriculumExamService;
        private readonly ITimeZoneService _timeZoneService;
        public ExamGeneratorController(
            ApplicationDbContext context,
            ICurriculumExamGeneratorService curriculumExamService, ITimeZoneService timeZoneService)
        {
            _context = context;
            _curriculumExamService = curriculumExamService;
            _timeZoneService = timeZoneService;
        }



        private string Sanitize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // إزالة الرموز التي تسبب أخطاء SQL
            string sanitized = input
                .Replace("$", "")
                .Replace(";", "")
                .Replace("--", "")
                .Replace("'", "''")   // حماية من SQL Injection
                .Replace("\"", "")
                .Replace("#", "")
                .Replace("<", "")
                .Replace(">", "")
                .Trim();

            return sanitized;
        }


        [HttpGet]
        [AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> CreateUnifiedExam()
        {
            var vm = new UnifiedExamCreationViewModel
            {
                Courses = await _context.Courses
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                    .ToListAsync(),

                Batches = await _context.Batches
                    .Where(b => !b.IsDeleted && !b.IsArchived)
                    .OrderBy(b => b.Name)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                    .ToListAsync(),

                QuestionPools = await _context.ProfessionalModels
                    .Where(m => !m.IsArchived && m.ModelType == QdratNew.Enums.ProfessionalModelType.Exam)
                    .OrderBy(m => m.Title)
                    .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Title })
                    .ToListAsync(),

                DurationMinutes = 30,
                QuestionCount = 40
            };

            return View(vm);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ExamAssignments", "Generate")]
        public async Task<IActionResult> CreateUnifiedExam(UnifiedExamCreationViewModel vm)
        {
            try
            {
                // تنظيف جميع المدخلات
                vm.Title = Safe(vm.Title);
                vm.ReferenceCode = Safe(vm.ReferenceCode);

                if (!ModelState.IsValid)
                {
                    TempData["Error"] = "⚠️ يرجى استكمال البيانات المطلوبة.";
                    return View(vm);
                }

                if (vm.SelectedBatchIds == null || !vm.SelectedBatchIds.Any())
                {
                    TempData["Error"] = "⚠️ يجب اختيار دفعة واحدة على الأقل.";
                    return View(vm);
                }

                var availableBatchIds = await _context.Batches
                    .AsNoTracking()
                    .Where(b => !b.IsDeleted && !b.IsArchived)
                    .Select(b => b.Id)
                    .ToListAsync();

                vm.SelectedBatchIds = vm.SelectedBatchIds
                    .Where(batchId => availableBatchIds.Any(id => id == batchId))
                    .Distinct()
                    .ToList();

                if (!vm.SelectedBatchIds.Any())
                {
                    TempData["Error"] = "⚠️ الدفعات المحددة غير متاحة أو مؤرشفة.";
                    return View(vm);
                }

                if (!vm.StartAt.HasValue || !vm.EndAt.HasValue)
                {
                    TempData["Error"] = "⚠️ يجب تحديد وقت بداية ونهاية الاختبار.";
                    return View(vm);
                }

                if (vm.GenerationMode == ExamGenerationMode.FromQuestionPools)
                {
                    var availableModelIds = await _context.ProfessionalModels
                        .AsNoTracking()
                        .Where(m => !m.IsArchived)
                        .Select(m => m.Id)
                        .ToListAsync();

                    vm.SelectedQuestionPoolIds = vm.SelectedQuestionPoolIds
                        .Where(modelId => availableModelIds.Any(id => id == modelId))
                        .Distinct()
                        .ToList();

                    if (!vm.SelectedQuestionPoolIds.Any())
                    {
                        TempData["Error"] = "⚠️ النماذج الاحترافية المحددة غير متاحة أو مؤرشفة.";
                        return View(vm);
                    }
                }

                var startUtc = _timeZoneService.ConvertToUtc(vm.StartAt.Value);
                var endUtc = _timeZoneService.ConvertToUtc(vm.EndAt.Value);

                if (endUtc <= startUtc)
                {
                    TempData["Error"] = "⚠️ يجب أن يكون وقت النهاية بعد البداية.";
                    return View(vm);
                }

                // تنظيف QuestionPools
                foreach (var p in vm.QuestionPools)
                    p.Text = Safe(p.Text);

                // تجهيز بنك الدروس
                List<int> lessonIds = new();

                if (vm.GenerationMode == ExamGenerationMode.AutoFromBank)
                {
                    var curriculumIds = await _context.CourseCurriculums
                        .Where(cc => cc.CourseId == vm.CourseId)
                        .Select(cc => cc.CurriculumId)
                        .ToListAsync();

                    var sectionIds = await _context.Sections
                        .Where(s => curriculumIds.Contains(s.CurriculumId))
                        .Select(s => s.Id)
                        .ToListAsync();

                    lessonIds = await _context.Lessons
                        .Where(l => sectionIds.Contains(l.SectionId))
                        .Select(l => l.Id)
                        .ToListAsync();
                }

                ExamAssignmentToBatch? lastAssignment = null;

                foreach (var batchId in vm.SelectedBatchIds)
                {
                    // إنشاء Exam
                    var examTitleRaw = string.IsNullOrWhiteSpace(vm.Title)
                        ? $"اختبار جديد للدفعة {batchId}"
                        : $"{vm.Title} - دفعة {batchId}";

                    var exam = new Exam
                    {
                        Title = Safe(examTitleRaw),
                        Type = vm.ExamType,
                        DurationMinutes = vm.DurationMinutes,
                        TotalQuestions = vm.QuestionCount,
                        CreatedAt = _timeZoneService.GetNowUtc(),
                        IsActive = true,
                        IsFromProfessionalModel = vm.GenerationMode == ExamGenerationMode.FromQuestionPools
                    };

                    _context.Exams.Add(exam);
                    await _context.SaveChangesAsync();

                    // إنشاء Assignment
                    var assignmentTitleRaw = exam.Title;
                    var assignment = new ExamAssignmentToBatch
                    {
                        ExamId = exam.Id,
                        BatchId = batchId,
                        Title = Safe(assignmentTitleRaw),
                        DurationMinutes = vm.DurationMinutes,
                        TotalQuestions = vm.QuestionCount,
                        CreatedAt = _timeZoneService.GetNowUtc(),
                        ScheduledDate = startUtc,
                        EndAt = endUtc,
                        IsOnline = !vm.IsInLab,
                        IsInLab = vm.IsInLab,
                        IsSentToStudents = false,
                        ReferenceCode = vm.IsInLab ? Safe(GenerateReferenceCode()) : null
                    };

                    _context.ExamAssignmentsToBatches.Add(assignment);
                    await _context.SaveChangesAsync();

                    // اختيار الأسئلة
                    List<Guid> selectedIds;

                    if (vm.GenerationMode == ExamGenerationMode.FromQuestionPools)
                    {
                        selectedIds = await _context.ProfessionalModelQuestions
                            .Where(m =>
                                vm.SelectedQuestionPoolIds.Contains(m.ModelId) &&
                                m.QuestionId != null)
                            .Select(m => m.QuestionId.Value)
                            .Distinct()
                            .OrderBy(x => Guid.NewGuid())  // بديل أمن لـ Random()
                            .Take(vm.QuestionCount)
                            .ToListAsync();
                    }
                    else
                    {
                        selectedIds = await _context.Questions
                            .Where(q => lessonIds.Contains(q.LessonId))
                            .Select(q => q.Id)
                            .Distinct()
                            .OrderBy(x => Guid.NewGuid())
                            .Take(vm.QuestionCount)
                            .ToListAsync();
                    }

                    int order = 1;
                    var examQuestions = selectedIds.Select(qId => new ExamQuestion
                    {
                        ExamId = exam.Id,
                        ExamAssignmentId = assignment.Id,
                        QuestionId = qId,
                        Order = order++,
                        IsManuallySelected = vm.GenerationMode == ExamGenerationMode.FromQuestionPools
                    });

                    _context.ExamQuestions.AddRange(examQuestions);
                    await _context.SaveChangesAsync();

                    lastAssignment = assignment;
                }

                TempData["Success"] = "✅ تم إنشاء الاختبار بنجاح.";
                return RedirectToAction("ConfirmSend", "ExamGenerator", new { area = "Admin", id = lastAssignment!.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
                return View(vm);
            }
        }







        [HttpGet]
        [AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> Details(int id)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(a => a.Exam)
                .Include(a => a.Batch)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            // جلب الأسئلة المرتبطة بالاختبار
            var questions = await _context.ExamQuestions
                .Include(q => q.Question)
                .Where(q => q.ExamAssignmentId == id)
                .OrderBy(q => q.Order)
                .ToListAsync();

            var vm = new ExamAssignmentDetailsViewModel
            {
                Assignment = assignment,
                Questions = questions
            };

            return View(vm);
        }





        [HttpGet]
        [AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> EditQuestions(int id)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            var questions = await _context.ExamQuestions
                .Include(q => q.Question)
                .Where(q => q.ExamAssignmentId == id)
                .OrderBy(q => q.Order)
                .ToListAsync();

            var vm = new ExamEditQuestionsViewModel
            {
                AssignmentId = id,
                AssignmentTitle = assignment.Title,
                Questions = questions.Select(q => new ExamQuestionRow
                {
                    Id = q.ExamId,
                    QuestionId = q.QuestionId,
                    Title = q.Question.Title,
                    Order = q.Order
                }).ToList()
            };

            return View(vm);
        }



        // ✅ مولّد الكود المرجعي الآمن
        private string GenerateReferenceCode()
        {
            var random = new Random();
            string code;

            do
            {
                // 🔥 رقم مكون من 6 خانات فقط
                code = random.Next(100000, 999999).ToString();
            }
            while (_context.ExamAssignmentsToBatches.Any(e => e.ReferenceCode == code));

            return code;
        }



        [HttpGet]
        [AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> ConfirmSend(int id)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(a => a.Exam)
                .Include(a => a.Batch)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            return View(assignment); // ✅ عرض صفحة التأكيد بدون أي إعادة توجيه
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ExamAssignments", "Publish")]
        public async Task<IActionResult> ConfirmSendConfirmed(int id)
        {
            var assignment = await _context.ExamAssignmentsToBatches.FindAsync(id);
            if (assignment == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على الاختبار.";
                return RedirectToAction("Index");
            }

            assignment.IsSentToStudents = true;
            assignment.AssignedAt = _timeZoneService.GetNowUtc();

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ تم إرسال الاختبار \"{assignment.Title}\" إلى دفعة {assignment.BatchId}.";
            return RedirectToAction("Details", new { id });
        }


        [HttpGet]
        [AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> Create()
        {
            var model = new ManualExamCreationViewModel();

            // تحميل المناهج كلها (ممكن لاحقًا تعمل فلترة حسب الدورة لو اختار CourseId)
            var curriculums = await _context.Curriculums.ToListAsync();
            model.CurriculumQuestionCounts = curriculums.Select(c => new CurriculumQuestionCountVm
            {
                CurriculumId = c.Id,
                CurriculumTitle = c.Title,
                QuestionCount = 0
            }).ToList();

            await LoadDropdowns(model);
            return View(model);
        }

        [HttpPost]
        [AdminPermission("ExamAssignments", "Generate")]
        public async Task<IActionResult> Create(ManualExamCreationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "⚠️ فشل حفظ البيانات. الرجاء التحقق من إدخال جميع الحقول المطلوبة.";
                await LoadDropdowns(model);
                return View(model);
            }

            try
            {
                var batchIsAvailable = await _context.Batches
                    .AsNoTracking()
                    .AnyAsync(b => b.Id == model.BatchId && !b.IsDeleted && !b.IsArchived);

                if (!batchIsAvailable)
                {
                    TempData["Error"] = "⚠️ الدفعة المحددة غير متاحة أو مؤرشفة.";
                    await LoadDropdowns(model);
                    return View(model);
                }

                int assignmentId = 0;

                // 🟦 توليد الاختبار (نفس منطقك الحالي)
                if (model.ExamType == ExamType.Course || model.ExamType == ExamType.GovernmentMock)
                {
                    var selectedCurriculums = model.CurriculumQuestionCounts
                        .Where(x => x.QuestionCount > 0)
                        .Select(x => (x.CurriculumId, x.QuestionCount))
                        .ToList();

                    assignmentId = await _curriculumExamService.GenerateMergedExamForCurriculumsAsync(
                        model.BatchId,
                        selectedCurriculums,
                        null,
                        model.ExamType
                    );
                }
                else
                {
                    assignmentId = await _curriculumExamService.GenerateExamForCurriculumAsync(
                        model.CurriculumId,
                        model.BatchId,
                        model.SectionId
                    );
                }

                if (assignmentId == 0)
                {
                    TempData["Error"] = "⚠️ لم يتم توليد الاختبار. قد لا توجد أسئلة كافية أو الشروط غير مكتملة.";
                    await LoadDropdowns(model);
                    return View(model);
                }

                // 🕒 تحديث معلومات التوقيت في جدول ExamAssignmentsToBatches
                var assignment = await _context.ExamAssignmentsToBatches
                    .Include(a => a.Exam)
                    .FirstOrDefaultAsync(a => a.Id == assignmentId);

                if (assignment != null)
                {
                    // ✅ تحويل كل القيم الزمنية إلى UTC قبل التخزين
                    var nowUtc = _timeZoneService.GetNowUtc();

                    // وقت البدء (اختياري من المستخدم أو الآن)
                    if (model.ScheduledDate.HasValue)
                    {
                        var utcDate = _timeZoneService.ConvertToUtc(model.ScheduledDate.Value);
                        assignment.ScheduledDate = utcDate;
                    }
                    else
                    {
                        assignment.ScheduledDate = nowUtc;
                    }

                    // وقت الإرسال والتوليد
                    assignment.AssignedAt = nowUtc;
                    assignment.CreatedAt = nowUtc;

                    // ✅ تحديد المدة (افتراضي 60 دقيقة إن لم تُحدد)
                    assignment.DurationMinutes = model.DurationMinutes > 0 ? model.DurationMinutes : 60;

                    // ✅ تعديل العنوان لو أدخله المستخدم
                    if (!string.IsNullOrWhiteSpace(model.Title))
                    {
                        assignment.Title = model.Title;
                        if (assignment.Exam != null)
                            assignment.Exam.Title = model.Title;
                    }

                    if (assignment.Exam != null)
                        assignment.Exam.RandomizeQuestions = model.RandomizeQuestions;

                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "✅ تم توليد الاختبار بنجاح وتخزين الوقت بتوقيت UTC.";
                return RedirectToAction("Details", "ExamAssignments", new { area = "Admin", id = assignmentId });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = $"❌ حدث خطأ أثناء التوليد: {inner}";
                await LoadDropdowns(model);
                return View(model);
            }
        }

   
        private async Task LoadDropdowns(ManualExamCreationViewModel model)
        {
            model.Batches = await _context.Batches
                .Where(b => !b.IsDeleted && !b.IsArchived)
                .OrderBy(b => b.Name)
                .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                .ToListAsync();

            model.Curriculums = await _context.Curriculums
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                .ToListAsync();

            model.Sections = await _context.Sections
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title })
                .ToListAsync();

            model.ExamTypes = Enum.GetValues(typeof(ExamType))
                .Cast<ExamType>()
                .Where(e => e != ExamType.LevelAssessment) // ✅ استبعاد اختبارات المستوى
                .Select(e => new SelectListItem
                {
                    Value = ((int)e).ToString(),
                    Text = e.GetType()
                            .GetMember(e.ToString())
                            .First()
                            .GetCustomAttribute<DisplayAttribute>()?.Name ?? e.ToString()
                })
                .ToList();
        }

        private string Safe(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            string x = input;

            // إزالة كل الرموز التي تسبب مشاكل SQL
            char[] banned = { '$', ';', '#', '<', '>', '`', '|', '{', '}', '[', ']' };
            foreach (char c in banned)
                x = x.Replace(c.ToString(), "");

            // معالجة الفاصلة المفردة مرة واحدة فقط
            x = x.Replace("'", "''");

            return x.Trim();
        }





        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ExamAssignments", "Generate")]
        public async Task<IActionResult> GenerateFromSection(int batchId, int sectionId)
        {
            try
            {
                var curriculumId = await _context.InstructorCurriculumBatches
                    .Where(icb => icb.BatchId == batchId)
                    .Select(icb => icb.CurriculumId)
                    .FirstOrDefaultAsync();

                if (curriculumId == 0)
                {
                    return RedirectToAction("AnalyzeSections", "ExamAssignments", new
                    {
                        area = "Admin",
                        batchId,
                        msg = "❌ لم يتم العثور على المنهج المرتبط بالدفعة.",
                        type = "error"
                    });
                }

                var assignmentId = await _curriculumExamService.GenerateExamForCurriculumAsync(
                    curriculumId, batchId, sectionId);

                if (assignmentId == 0)
                {
                    return RedirectToAction("AnalyzeSections", "ExamAssignments", new
                    {
                        area = "Admin",
                        batchId,
                        msg = "⚠️ لم يتم توليد الاختبار، ربما لا توجد أسئلة كافية.",
                        type = "warning"
                    });
                }

                return RedirectToAction("Details", "ExamAssignments", new
                {
                    area = "Admin",
                    id = assignmentId,
                    msg = "✅ تم توليد الاختبار بنجاح.",
                    type = "success"
                });
            }
            catch (Exception ex)
            {
                return RedirectToAction("AnalyzeSections", "ExamAssignments", new
                {
                    area = "Admin",
                    batchId,
                    msg = $"❌ خطأ داخلي أثناء التوليد: {ex.Message}",
                    type = "error"
                });
            }
        }
    }
}
