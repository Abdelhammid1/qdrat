using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.Security;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.EnhancementSkills;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee,Instructor")]
    public class EnhancementSkillsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAdvancedNotificationService _notificationService;

        public EnhancementSkillsController(
            ApplicationDbContext context,
            IAdvancedNotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // =====================================================================
        // INDEX — الداشبورد الرئيسي
        // =====================================================================
        [HttpGet]
        [AdminPermission("EnhancementSkills", "Read")]
        public async Task<IActionResult> Index()
        {
            // جلب كل المجموعات (نشطة + مؤرشفة)
            var allSets = await _context.EnhancementSkillSets
                .AsNoTracking()
                .Include(s => s.Batch).ThenInclude(b => b.Course)
                .Include(s => s.ProfessionalModel)
                .Include(s => s.Indicators).ThenInclude(i => i.Lesson).ThenInclude(l => l.Section)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var allSetIds = allSets.Select(s => s.Id).ToList();

            // إحصاءات التعيينات
            var assignmentStats = await _context.EnhancementSkillAssignments
                .AsNoTracking()
                .Where(a => allSetIds.Contains(a.EnhancementSkillSetId))
                .GroupBy(a => a.EnhancementSkillSetId)
                .Select(g => new
                {
                    SetId = g.Key,
                    TotalAssignments = g.Count(),
                    Answered = g.Count(a => a.AnsweredAt != null),
                    Students = g.Select(a => a.StudentId).Distinct().Count()
                })
                .ToListAsync();

            EnhancementSkillSetViewModel ToVm(EnhancementSkillSet s)
            {
                var stats = assignmentStats.FirstOrDefault(a => a.SetId == s.Id);
                var sectionTitle = s.GenerationMethod == 1
                    ? string.Join("، ", s.Indicators
                        .Select(i => i.Lesson?.Section?.Title ?? "")
                        .Where(t => !string.IsNullOrEmpty(t)).Distinct())
                    : s.ProfessionalModel?.Title ?? "نموذج احترافي";

                return new EnhancementSkillSetViewModel
                {
                    Id = s.Id,
                    BatchId = s.BatchId,
                    BatchName = s.Batch?.Name ?? "—",
                    LectureTitle = sectionTitle,
                    Title = s.Title,
                    Description = s.Description,
                    CreatedAt = s.CreatedAt,
                    EndAt = s.EndAt,
                    SentAt = s.SentAt,
                    ScheduledSendAt = s.ScheduledSendAt,
                    QuestionsPerStudent = s.QuestionsPerStudent,
                    IsSent = s.IsSent,
                    IsArchived = s.IsArchived,
                    GenerationMethod = s.GenerationMethod,
                    StudentsCount = stats?.Students ?? 0,
                    AnsweredCount = stats?.Answered ?? 0,
                    QuestionsCount = stats?.TotalAssignments ?? 0
                };
            }

            var activeSets = allSets.Where(s => !s.IsArchived).Select(ToVm).ToList();
            var archivedSets = allSets.Where(s => s.IsArchived).Select(ToVm).ToList();

            // كروت الدفعات — تشمل كل الدفعات التي لها مجموعات
            var batchIds = allSets.Select(s => s.BatchId).Distinct().ToHashSet();
            var batchInfo = await _context.Batches
                .AsNoTracking()
                .Where(b => batchIds.Contains(b.Id))
                .Include(b => b.Course)
                .ToListAsync();

            // عدد طلاب كل دفعة
            var enrollCounts = await _context.StudentBatchEnrollments
                .Where(e => batchIds.Contains(e.BatchId))
                .GroupBy(e => e.BatchId)
                .Select(g => new { BatchId = g.Key, Count = g.Count() })
                .ToListAsync();

            var batchCards = batchIds.Select(bid =>
            {
                var b = batchInfo.FirstOrDefault(x => x.Id == bid);
                var batchActiveSets = activeSets.Where(s => s.BatchId == bid).ToList();
                var batchArchivedSets = archivedSets.Where(s => s.BatchId == bid).ToList();
                var allBatchStats = assignmentStats
                    .Where(a => allSets.Any(s => s.Id == a.SetId && s.BatchId == bid))
                    .ToList();

                return new EnhancementBatchCardVm
                {
                    BatchId = bid,
                    BatchName = b?.Name ?? "—",
                    CourseTitle = b?.Course?.Name ?? "—",
                    TotalStudents = enrollCounts.FirstOrDefault(e => e.BatchId == bid)?.Count ?? 0,
                    TotalSets = batchActiveSets.Count,
                    SentSets = batchActiveSets.Count(s => s.IsSent),
                    PendingSets = batchActiveSets.Count(s => !s.IsSent),
                    ArchivedSets = batchArchivedSets.Count,
                    TotalAssignments = allBatchStats.Sum(a => a.TotalAssignments),
                    AnsweredAssignments = allBatchStats.Sum(a => a.Answered)
                };
            }).OrderByDescending(b => b.TotalSets).ToList();

            var totalAssigned = assignmentStats.Sum(a => a.TotalAssignments);
            var totalAnswered = assignmentStats.Sum(a => a.Answered);

            var vm = new EnhancementIndexVm
            {
                BatchCards = batchCards,
                Sets = activeSets,
                ArchivedSets = archivedSets,
                TotalSets = activeSets.Count,
                SentSets = activeSets.Count(s => s.IsSent),
                PendingSets = activeSets.Count(s => !s.IsSent),
                ArchivedCount = archivedSets.Count,
                TotalStudentsReached = assignmentStats.Sum(a => a.Students),
                OverallResponseRate = totalAssigned > 0
                    ? (int)Math.Round(totalAnswered * 100.0 / totalAssigned) : 0
            };

            return View(vm);
        }

        // =====================================================================
        // BATCH DETAILS — مهارات دفعة معينة
        // =====================================================================
        [HttpGet]
        [AdminPermission("EnhancementSkills", "Read")]
        public async Task<IActionResult> BatchDetails(int batchId)
        {
            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null) return NotFound();

            var allSets = await _context.EnhancementSkillSets
                .AsNoTracking()
                .Include(s => s.ProfessionalModel)
                .Include(s => s.Indicators).ThenInclude(i => i.Lesson).ThenInclude(l => l.Section)
                .Where(s => s.BatchId == batchId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var setIds = allSets.Select(s => s.Id).ToList();
            var stats = await _context.EnhancementSkillAssignments
                .AsNoTracking()
                .Where(a => setIds.Contains(a.EnhancementSkillSetId))
                .GroupBy(a => a.EnhancementSkillSetId)
                .Select(g => new
                {
                    SetId = g.Key,
                    TotalAssignments = g.Count(),
                    Answered = g.Count(a => a.AnsweredAt != null),
                    Students = g.Select(a => a.StudentId).Distinct().Count()
                })
                .ToListAsync();

            EnhancementSkillSetViewModel ToVm(EnhancementSkillSet s)
            {
                var st = stats.FirstOrDefault(a => a.SetId == s.Id);
                var src = s.GenerationMethod == 1
                    ? string.Join("، ", s.Indicators.Select(i => i.Lesson?.Section?.Title ?? "").Where(t => !string.IsNullOrEmpty(t)).Distinct())
                    : s.ProfessionalModel?.Title ?? "نموذج احترافي";
                return new EnhancementSkillSetViewModel
                {
                    Id = s.Id, BatchId = s.BatchId, BatchName = batch.Name,
                    LectureTitle = src, Title = s.Title, Description = s.Description,
                    CreatedAt = s.CreatedAt, EndAt = s.EndAt, SentAt = s.SentAt,
                    ScheduledSendAt = s.ScheduledSendAt, QuestionsPerStudent = s.QuestionsPerStudent,
                    IsSent = s.IsSent, IsArchived = s.IsArchived, GenerationMethod = s.GenerationMethod,
                    StudentsCount = st?.Students ?? 0, AnsweredCount = st?.Answered ?? 0,
                    QuestionsCount = st?.TotalAssignments ?? 0
                };
            }

            var enrollCount = await _context.StudentBatchEnrollments
                .CountAsync(e => e.BatchId == batchId);

            var vm = new BatchEnhancementDetailsVm
            {
                BatchId = batchId,
                BatchName = batch.Name,
                CourseTitle = batch.Course?.Name ?? "—",
                TotalStudents = enrollCount,
                Sets = allSets.Where(s => !s.IsArchived).Select(ToVm).ToList(),
                ArchivedSets = allSets.Where(s => s.IsArchived).Select(ToVm).ToList()
            };

            return View(vm);
        }

        // =====================================================================
        // CREATE GET
        // =====================================================================
        [HttpGet]
        [AdminPermission("EnhancementSkills", "Create")]
        public async Task<IActionResult> Create()
        {
            var vm = new CreateEnhancementSetViewModel
            {
                Batches = await _context.Batches
                    .Where(b => !b.IsDeleted && !b.IsArchived)
                    .OrderByDescending(b => b.StartDate)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                    .ToListAsync(),

                ProfessionalModels = await _context.ProfessionalModels
                    .Where(m => !m.IsArchived)
                    .OrderBy(m => m.Title)
                    .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = $"{m.Title} — {m.Description.Substring(0, Math.Min(50, m.Description.Length))}" })
                    .ToListAsync()
            };
            return View(vm);
        }

        // =====================================================================
        // AJAX — تحميل محاور المنهج للدفعة
        // =====================================================================
        [HttpGet]
        [AdminPermission("EnhancementSkills", "Create")]
        public async Task<IActionResult> GetSectionsForBatch(int batchId)
        {
            var batch = await _context.Batches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId);
            if (batch == null) return Json(new List<object>());

            var curriculumIds = await _context.CourseCurriculums
                .Where(cc => cc.CourseId == batch.CourseId)
                .Select(cc => cc.CurriculumId)
                .ToListAsync();

            var sections = await _context.Sections
                .AsNoTracking()
                .Where(s => curriculumIds.Contains(s.CurriculumId))
                .OrderBy(s => s.Title)
                .Select(s => new { id = s.Id, title = s.Title })
                .ToListAsync();

            return Json(sections);
        }

        // =====================================================================
        // AJAX — تحميل المؤشرات (دروس) لمحور معين مع عدد أسئلة التعزيز
        // =====================================================================
        [HttpGet]
        [AdminPermission("EnhancementSkills", "Create")]
        public async Task<IActionResult> GetLessonsForSection(int sectionId)
        {
            var lessons = await _context.Lessons
                .AsNoTracking()
                .Where(l => l.SectionId == sectionId && l.IsActive)
                .OrderBy(l => l.Title)
                .Select(l => new { id = l.Id, title = l.Title })
                .ToListAsync();

            var lessonIds = lessons.Select(l => l.id).ToList();

            var questionCounts = await _context.Questions
                .Where(q => lessonIds.Contains(q.LessonId)
                         && q.IsReviewed && !q.IsRejected
                         && (q.UsageTypes & QuestionUsageType.Enhancement) == QuestionUsageType.Enhancement)
                .GroupBy(q => q.LessonId)
                .Select(g => new { lessonId = g.Key, count = g.Count() })
                .ToListAsync();

            var result = lessons.Select(l => new
            {
                id = l.id,
                title = l.title,
                availableCount = questionCounts.FirstOrDefault(c => c.lessonId == l.id)?.count ?? 0
            });

            return Json(result);
        }

        // =====================================================================
        // AJAX — أسئلة النموذج الاحترافي
        // =====================================================================
        [HttpGet]
        [AdminPermission("EnhancementSkills", "Create")]
        public async Task<IActionResult> GetModelQuestionsCount(int modelId)
        {
            var count = await _context.ProfessionalModelQuestions
                .CountAsync(q => q.ModelId == modelId && q.QuestionId != null);
            return Json(new { count });
        }

        // =====================================================================
        // CREATE POST
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("EnhancementSkills", "Create")]
        public async Task<IActionResult> Create(CreateEnhancementSetViewModel vm)
        {
            await FillCreateVmListsAsync(vm);

            if (vm.BatchId <= 0)
            {
                ModelState.AddModelError("", "يرجى اختيار الدفعة.");
                return View(vm);
            }

            // تحقق الطريقة الأولى
            if (vm.GenerationMethod == 1)
            {
                var validIndicators = vm.SelectedIndicators?
                    .Where(i => i.LessonId > 0 && i.QuestionCount > 0)
                    .ToList() ?? new List<IndicatorInputItem>();

                if (!validIndicators.Any())
                {
                    ModelState.AddModelError("", "يرجى اختيار محور واحد على الأقل وتحديد عدد الأسئلة لمؤشراته.");
                    return View(vm);
                }

                var studentIds = await GetBatchStudentIds(vm.BatchId);
                if (!studentIds.Any())
                {
                    ModelState.AddModelError("", "لا يوجد طلاب في هذه الدفعة.");
                    return View(vm);
                }

                var questionsPerStudent = validIndicators.Sum(i => i.QuestionCount);

                var set = new EnhancementSkillSet
                {
                    BatchId = vm.BatchId,
                    Title = string.IsNullOrWhiteSpace(vm.Title) ? "مهارات تعزيزية" : vm.Title.Trim(),
                    Description = vm.Description?.Trim(),
                    GenerationMethod = 1,
                    QuestionsPerStudent = questionsPerStudent,
                    EndAt = vm.EndAt,
                    ScheduledSendAt = vm.ScheduledSendAt,
                    CreatedAt = DateTime.Now
                };
                _context.EnhancementSkillSets.Add(set);
                await _context.SaveChangesAsync();

                // حفظ المؤشرات المختارة
                foreach (var ind in validIndicators)
                {
                    _context.EnhancementSetIndicators.Add(new EnhancementSetIndicator
                    {
                        EnhancementSkillSetId = set.Id,
                        LessonId = ind.LessonId,
                        QuestionCount = ind.QuestionCount
                    });
                }
                await _context.SaveChangesAsync();

                // توليد الأسئلة للطلاب
                await AssignByIndicatorsAsync(set.Id, validIndicators, studentIds);

                TempData["Success"] = "✅ تم إنشاء المجموعة التعزيزية من المحاور والمؤشرات. راجع الأسئلة قبل الإرسال.";
                return RedirectToAction(nameof(Details), new { id = set.Id });
            }
            else // الطريقة الثانية: نموذج احترافي
            {
                if (!vm.ProfessionalModelId.HasValue || vm.ProfessionalModelId <= 0)
                {
                    ModelState.AddModelError("", "يرجى اختيار النموذج الاحترافي.");
                    return View(vm);
                }
                if (vm.QuestionsPerStudent <= 0)
                {
                    ModelState.AddModelError("", "يرجى تحديد عدد الأسئلة لكل طالب.");
                    return View(vm);
                }

                var studentIds = await GetBatchStudentIds(vm.BatchId);
                if (!studentIds.Any())
                {
                    ModelState.AddModelError("", "لا يوجد طلاب في هذه الدفعة.");
                    return View(vm);
                }

                var set = new EnhancementSkillSet
                {
                    BatchId = vm.BatchId,
                    Title = string.IsNullOrWhiteSpace(vm.Title) ? "مهارات تعزيزية" : vm.Title.Trim(),
                    Description = vm.Description?.Trim(),
                    GenerationMethod = 2,
                    ProfessionalModelId = vm.ProfessionalModelId,
                    QuestionsPerStudent = vm.QuestionsPerStudent,
                    EndAt = vm.EndAt,
                    ScheduledSendAt = vm.ScheduledSendAt,
                    CreatedAt = DateTime.Now
                };
                _context.EnhancementSkillSets.Add(set);
                await _context.SaveChangesAsync();

                await AssignFromModelAsync(set.Id, vm.ProfessionalModelId.Value, studentIds, vm.QuestionsPerStudent);

                TempData["Success"] = "✅ تم إنشاء المجموعة التعزيزية من النموذج الاحترافي. راجع الأسئلة قبل الإرسال.";
                return RedirectToAction(nameof(Details), new { id = set.Id });
            }
        }

        // =====================================================================
        // DETAILS
        // =====================================================================
        [HttpGet]
        [AdminPermission("EnhancementSkills", "Read")]
        public async Task<IActionResult> Details(int id)
        {
            var set = await _context.EnhancementSkillSets
                .AsNoTracking()
                .Include(s => s.Batch)
                .Include(s => s.ProfessionalModel)
                .Include(s => s.Indicators)
                    .ThenInclude(i => i.Lesson)
                    .ThenInclude(l => l.Section)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (set == null) return NotFound();

            var assignments = await _context.EnhancementSkillAssignments
                .AsNoTracking()
                .Where(a => a.EnhancementSkillSetId == id)
                .Include(a => a.Student)
                .Include(a => a.Question)
                    .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Section)
                .ToListAsync();

            // تجميع الطلاب
            var studentRows = assignments
                .GroupBy(a => a.StudentId)
                .Select(g =>
                {
                    var first = g.First();
                    return new EnhancementStudentRowVm
                    {
                        StudentId = g.Key,
                        FullName = first.Student?.FullName ?? "—",
                        PhoneNumber = first.Student?.PhoneNumber,
                        TotalQuestions = g.Count(),
                        AnsweredQuestions = g.Count(a => a.AnsweredAt != null),
                        CorrectAnswers = g.Count(a => a.IsCorrect == true)
                    };
                })
                .OrderBy(r => r.FullName)
                .ToList();

            // أسئلة المراجعة (مجمعة بدون تكرار)
            var reviewQuestions = assignments
                .Select(a => a.Question)
                .Where(q => q != null)
                .GroupBy(q => q!.Id)
                .Select(g =>
                {
                    var q = g.First()!;
                    return new EnhancementQuestionReviewVm
                    {
                        QuestionId = q.Id,
                        Title = q.Title ?? "",
                        LessonTitle = q.Lesson?.Title ?? "—",
                        SectionTitle = q.Lesson?.Section?.Title ?? "—",
                        Difficulty = q.Difficulty.ToString()
                    };
                })
                .ToList();

            // تفصيل المؤشرات
            var indicatorSummaries = set.Indicators.Select(i => new EnhancementIndicatorSummaryVm
            {
                LessonId = i.LessonId,
                LessonTitle = i.Lesson?.Title ?? "—",
                SectionTitle = i.Lesson?.Section?.Title ?? "—",
                RequestedCount = i.QuestionCount,
                ActualCount = reviewQuestions
                    .Count(q => q.LessonTitle == (i.Lesson?.Title ?? "")),
                Questions = reviewQuestions
                    .Where(q => q.LessonTitle == (i.Lesson?.Title ?? ""))
                    .ToList()
            }).ToList();

            var vm = new EnhancementDetailsVm
            {
                SetId = set.Id,
                Title = set.Title,
                Description = set.Description,
                BatchName = set.Batch?.Name ?? "—",
                LectureTitle = set.GenerationMethod == 1
                    ? string.Join(" | ", set.Indicators.Select(i => i.Lesson?.Section?.Title).Where(t => t != null).Distinct())
                    : set.ProfessionalModel?.Title ?? "نموذج احترافي",
                QuestionsPerStudent = set.QuestionsPerStudent,
                IsSent = set.IsSent,
                CreatedAt = set.CreatedAt,
                SentAt = set.SentAt,
                ScheduledSendAt = set.ScheduledSendAt,
                EndAt = set.EndAt,
                GenerationMethod = set.GenerationMethod,
                ProfessionalModelTitle = set.ProfessionalModel?.Title,
                Students = studentRows,
                ReviewQuestions = reviewQuestions,
                IndicatorSummaries = indicatorSummaries
            };

            return View(vm);
        }

        // =====================================================================
        // EDIT GET
        // =====================================================================
        [HttpGet]
        [AdminPermission("EnhancementSkills", "Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var set = await _context.EnhancementSkillSets
                .AsNoTracking()
                .Include(s => s.Batch)
                .Include(s => s.ProfessionalModel)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (set == null) return NotFound();

            var vm = new EditEnhancementSetViewModel
            {
                Id = set.Id,
                Title = set.Title,
                Description = set.Description,
                QuestionsPerStudent = set.QuestionsPerStudent,
                EndAt = set.EndAt,
                ScheduledSendAt = set.ScheduledSendAt,
                BatchName = set.Batch?.Name ?? "—",
                LectureTitle = set.ProfessionalModel?.Title ?? "محاور/مؤشرات",
                IsSent = set.IsSent
            };
            return View(vm);
        }

        // =====================================================================
        // EDIT POST
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("EnhancementSkills", "Edit")]
        public async Task<IActionResult> Edit(EditEnhancementSetViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var set = await _context.EnhancementSkillSets.FindAsync(vm.Id);
            if (set == null) return NotFound();

            set.Title = vm.Title.Trim();
            set.Description = vm.Description?.Trim();
            set.QuestionsPerStudent = vm.QuestionsPerStudent;
            set.EndAt = vm.EndAt;
            set.ScheduledSendAt = vm.ScheduledSendAt;
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم تحديث بيانات المجموعة بنجاح.";
            return RedirectToAction(nameof(Details), new { id = vm.Id });
        }

        // =====================================================================
        // SEND
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("EnhancementSkills", "Send")]
        public async Task<IActionResult> Send(int id)
        {
            var set = await _context.EnhancementSkillSets
                .Include(s => s.Batch)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (set == null) return NotFound();

            var studentIds = await _context.EnhancementSkillAssignments
                .Where(a => a.EnhancementSkillSetId == id)
                .Select(a => a.StudentId).Distinct().ToListAsync();

            if (!studentIds.Any())
            {
                TempData["Error"] = "⚠️ لا توجد أسئلة مضافة لهذه المجموعة.";
                return RedirectToAction(nameof(Details), new { id });
            }

            set.IsSent = true;
            set.SentAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _notificationService.SendToStudentsAsync(
                studentIds,
                $"📘 تم إرسال مهارات تعزيزية جديدة — {set.Title}. تأكد من حلها في الوقت المحدد.",
                NotificationCategory.Homework,
                "/Students/EnhancementSkills"
            );

            TempData["Success"] = $"✅ تم إرسال المهارات التعزيزية إلى {studentIds.Count} طالب.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // =====================================================================
        // RESEND TO STUDENT
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("EnhancementSkills", "Resend")]
        public async Task<IActionResult> ResendToStudent(int setId, int studentId, int questionsCount = 5)
        {
            var set = await _context.EnhancementSkillSets
                .Include(s => s.Indicators)
                .FirstOrDefaultAsync(s => s.Id == setId);
            if (set == null) return NotFound();

            var old = await _context.EnhancementSkillAssignments
                .Where(a => a.EnhancementSkillSetId == setId && a.StudentId == studentId)
                .ToListAsync();
            _context.EnhancementSkillAssignments.RemoveRange(old);
            await _context.SaveChangesAsync();

            if (set.GenerationMethod == 1 && set.Indicators.Any())
            {
                var indicators = set.Indicators.Select(i => new IndicatorInputItem
                {
                    LessonId = i.LessonId,
                    QuestionCount = i.QuestionCount
                }).ToList();
                await AssignByIndicatorsAsync(setId, indicators, new List<int> { studentId });
            }
            else if (set.GenerationMethod == 2 && set.ProfessionalModelId.HasValue)
            {
                await AssignFromModelAsync(setId, set.ProfessionalModelId.Value, new List<int> { studentId }, questionsCount);
            }

            await _notificationService.SendToStudentAsync(
                studentId,
                "🔄 تم إعادة إرسال المهارات التعزيزية. يرجى الدخول وحلها.",
                NotificationCategory.Homework,
                "/Students/EnhancementSkills"
            );

            TempData["Success"] = "✅ تمت إعادة إرسال الأسئلة للطالب.";
            return RedirectToAction(nameof(Details), new { id = setId });
        }

        // =====================================================================
        // REGENERATE — إعادة توليد أسئلة جديدة للمجموعة (قبل الإرسال)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("EnhancementSkills", "Edit")]
        public async Task<IActionResult> Regenerate(int id)
        {
            var set = await _context.EnhancementSkillSets
                .Include(s => s.Indicators)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (set == null) return NotFound();
            if (set.IsSent)
            {
                TempData["Error"] = "⚠️ لا يمكن إعادة التوليد بعد الإرسال.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var old = await _context.EnhancementSkillAssignments
                .Where(a => a.EnhancementSkillSetId == id).ToListAsync();
            _context.EnhancementSkillAssignments.RemoveRange(old);
            await _context.SaveChangesAsync();

            var studentIds = await GetBatchStudentIds(set.BatchId);

            if (set.GenerationMethod == 1)
            {
                var indicators = set.Indicators.Select(i => new IndicatorInputItem
                {
                    LessonId = i.LessonId,
                    QuestionCount = i.QuestionCount
                }).ToList();
                await AssignByIndicatorsAsync(id, indicators, studentIds);
            }
            else if (set.GenerationMethod == 2 && set.ProfessionalModelId.HasValue)
            {
                await AssignFromModelAsync(id, set.ProfessionalModelId.Value, studentIds, set.QuestionsPerStudent);
            }

            TempData["Success"] = "🔄 تم إعادة توليد الأسئلة بنجاح.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // =====================================================================
        // DELETE
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("EnhancementSkills", "Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var set = await _context.EnhancementSkillSets
                .Include(s => s.Assignments)
                .Include(s => s.Indicators)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (set == null) return NotFound();

            _context.EnhancementSkillAssignments.RemoveRange(set.Assignments);
            _context.EnhancementSetIndicators.RemoveRange(set.Indicators);
            _context.EnhancementSkillSets.Remove(set);
            await _context.SaveChangesAsync();

            TempData["Success"] = "🗑️ تم حذف المجموعة التعزيزية.";
            return RedirectToAction(nameof(Index));
        }

        // =====================================================================
        // ARCHIVE / RESTORE
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("EnhancementSkills", "Archive")]
        public async Task<IActionResult> Archive(int id)
        {
            var set = await _context.EnhancementSkillSets.FindAsync(id);
            if (set == null) return NotFound();
            set.IsArchived = true;
            set.ArchivedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            TempData["Success"] = "📦 تم أرشفة المجموعة.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("EnhancementSkills", "Archive")]
        public async Task<IActionResult> Restore(int id)
        {
            var set = await _context.EnhancementSkillSets.FindAsync(id);
            if (set == null) return NotFound();
            set.IsArchived = false;
            set.ArchivedAt = null;
            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ تم استرجاع المجموعة.";
            return RedirectToAction(nameof(Index));
        }

        // =====================================================================
        // AJAX — معاينة سؤال (Partial)
        // =====================================================================
        [HttpGet]
        [AdminPermission("EnhancementSkills", "Read")]
        public async Task<IActionResult> PreviewQuestion(Guid id)
        {
            var question = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Section)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
                return Content("<div class='text-danger text-center p-4'>❌ لم يتم العثور على السؤال.</div>");

            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", question.ToDisplayModel());
        }

        // =====================================================================
        // AJAX — جلب أسئلة بنك الأسئلة للمؤشر (للاستبدال أو الإضافة)
        // =====================================================================
        [HttpGet]
        [AdminPermission("EnhancementSkills", "Edit")]
        public async Task<IActionResult> GetBankQuestions(int lessonId, int setId, string? replaceQuestionId = null)
        {
            // الأسئلة المعتمدة التعزيزية لهذا المؤشر
            var bankQuestions = await _context.Questions
                .AsNoTracking()
                .Where(q => q.LessonId == lessonId
                         && q.IsReviewed && !q.IsRejected
                         && (q.UsageTypes & QuestionUsageType.Enhancement) == QuestionUsageType.Enhancement)
                .OrderBy(q => q.Difficulty)
                .Select(q => new { q.Id, q.Title, q.Difficulty })
                .ToListAsync();

            // الأسئلة المُعيَّنة حالياً في هذه المجموعة
            var assignedIds = await _context.EnhancementSkillAssignments
                .AsNoTracking()
                .Where(a => a.EnhancementSkillSetId == setId)
                .Select(a => a.QuestionId)
                .Distinct()
                .ToListAsync();

            // عند الاستبدال: السؤال القديم يُعتبر غير مُعيَّن (متاح للاستبدال به)
            Guid? replaceGuid = null;
            if (!string.IsNullOrEmpty(replaceQuestionId) && Guid.TryParse(replaceQuestionId, out var rg))
                replaceGuid = rg;

            var result = bankQuestions.Select(q => new
            {
                id = q.Id,
                title = q.Title ?? "",
                difficulty = q.Difficulty.ToString(),
                alreadyAssigned = assignedIds.Contains(q.Id) && q.Id != replaceGuid
            });

            return Json(result);
        }

        // =====================================================================
        // AJAX POST — استبدال سؤال بآخر من بنك الأسئلة
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("EnhancementSkills", "Edit")]
        public async Task<IActionResult> ReplaceQuestion(int setId, string oldQuestionId, string newQuestionId)
        {
            if (!Guid.TryParse(oldQuestionId, out var oldId) || !Guid.TryParse(newQuestionId, out var newId))
                return Json(new { success = false, message = "معرّف السؤال غير صحيح." });

            var set = await _context.EnhancementSkillSets.FindAsync(setId);
            if (set == null) return Json(new { success = false, message = "المجموعة غير موجودة." });
            if (set.IsSent) return Json(new { success = false, message = "لا يمكن التعديل بعد الإرسال." });

            var affected = await _context.EnhancementSkillAssignments
                .Where(a => a.EnhancementSkillSetId == setId && a.QuestionId == oldId)
                .ToListAsync();

            if (!affected.Any())
                return Json(new { success = false, message = "السؤال القديم غير موجود في هذه المجموعة." });

            foreach (var a in affected)
                a.QuestionId = newId;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"تم استبدال السؤال لـ {affected.Count} طالب." });
        }

        // =====================================================================
        // AJAX POST — إضافة سؤال جديد من البنك لمؤشر معين
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("EnhancementSkills", "Edit")]
        public async Task<IActionResult> AddQuestionFromBank(int setId, int lessonId, string questionId)
        {
            if (!Guid.TryParse(questionId, out var qGuid))
                return Json(new { success = false, message = "معرّف السؤال غير صحيح." });

            var set = await _context.EnhancementSkillSets.FindAsync(setId);
            if (set == null) return Json(new { success = false, message = "المجموعة غير موجودة." });
            if (set.IsSent) return Json(new { success = false, message = "لا يمكن الإضافة بعد الإرسال." });

            // جلب طلاب المجموعة من التعيينات الحالية
            var studentIds = await _context.EnhancementSkillAssignments
                .Where(a => a.EnhancementSkillSetId == setId)
                .Select(a => a.StudentId)
                .Distinct()
                .ToListAsync();

            if (!studentIds.Any())
                return Json(new { success = false, message = "لا يوجد طلاب في هذه المجموعة." });

            // تجنّب التكرار: استبعاد الطلاب الذين لديهم هذا السؤال مسبقاً
            var alreadyHave = await _context.EnhancementSkillAssignments
                .Where(a => a.EnhancementSkillSetId == setId && a.QuestionId == qGuid)
                .Select(a => a.StudentId)
                .ToListAsync();

            var toAdd = studentIds.Except(alreadyHave).ToList();
            if (!toAdd.Any())
                return Json(new { success = false, message = "السؤال مُضاف بالفعل لجميع الطلاب." });

            var now = DateTime.Now;
            foreach (var sid in toAdd)
            {
                _context.EnhancementSkillAssignments.Add(new EnhancementSkillAssignment
                {
                    EnhancementSkillSetId = setId,
                    StudentId = sid,
                    QuestionId = qGuid,
                    AssignedAt = now
                });
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"تمت إضافة السؤال لـ {toAdd.Count} طالب." });
        }

        // =====================================================================
        // AJAX POST — حذف سؤال من المؤشر (من جميع الطلاب)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("EnhancementSkills", "Edit")]
        public async Task<IActionResult> RemoveQuestion(int setId, string questionId)
        {
            if (!Guid.TryParse(questionId, out var qGuid))
                return Json(new { success = false, message = "معرّف السؤال غير صحيح." });

            var set = await _context.EnhancementSkillSets.FindAsync(setId);
            if (set == null) return Json(new { success = false, message = "المجموعة غير موجودة." });
            if (set.IsSent) return Json(new { success = false, message = "لا يمكن الحذف بعد الإرسال." });

            var toRemove = await _context.EnhancementSkillAssignments
                .Where(a => a.EnhancementSkillSetId == setId && a.QuestionId == qGuid)
                .ToListAsync();

            if (!toRemove.Any())
                return Json(new { success = false, message = "السؤال غير موجود في هذه المجموعة." });

            _context.EnhancementSkillAssignments.RemoveRange(toRemove);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"تم حذف السؤال من {toRemove.Count} تعيين." });
        }

        // =====================================================================
        // PRIVATE HELPERS
        // =====================================================================

        private async Task<List<int>> GetBatchStudentIds(int batchId)
        {
            return await _context.StudentBatchEnrollments
                .Where(e => e.BatchId == batchId)
                .Select(e => e.StudentID)
                .ToListAsync();
        }

        private async Task AssignByIndicatorsAsync(int setId, List<IndicatorInputItem> indicators, List<int> studentIds)
        {
            var rng = new Random();
            var now = DateTime.Now;

            foreach (var ind in indicators)
            {
                var questions = await _context.Questions
                    .AsNoTracking()
                    .Where(q => q.LessonId == ind.LessonId
                             && q.IsReviewed && !q.IsRejected
                             && (q.UsageTypes & QuestionUsageType.Enhancement) == QuestionUsageType.Enhancement)
                    .ToListAsync();

                if (!questions.Any()) continue;

                foreach (var studentId in studentIds)
                {
                    var selected = questions.OrderBy(_ => rng.Next()).Take(ind.QuestionCount).ToList();
                    foreach (var q in selected)
                    {
                        _context.EnhancementSkillAssignments.Add(new EnhancementSkillAssignment
                        {
                            EnhancementSkillSetId = setId,
                            StudentId = studentId,
                            QuestionId = q.Id,
                            AssignedAt = now
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task AssignFromModelAsync(int setId, int modelId, List<int> studentIds, int questionsPerStudent)
        {
            var modelQuestions = await _context.ProfessionalModelQuestions
                .AsNoTracking()
                .Where(mq => mq.ModelId == modelId && mq.QuestionId != null)
                .OrderBy(mq => mq.OrderNumber)
                .Select(mq => mq.QuestionId!.Value)
                .ToListAsync();

            if (!modelQuestions.Any()) return;

            var rng = new Random();
            var now = DateTime.Now;

            foreach (var studentId in studentIds)
            {
                var selected = modelQuestions.OrderBy(_ => rng.Next()).Take(questionsPerStudent).ToList();
                foreach (var qId in selected)
                {
                    _context.EnhancementSkillAssignments.Add(new EnhancementSkillAssignment
                    {
                        EnhancementSkillSetId = setId,
                        StudentId = studentId,
                        QuestionId = qId,
                        AssignedAt = now
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task FillCreateVmListsAsync(CreateEnhancementSetViewModel vm)
        {
            vm.Batches = await _context.Batches
                .Where(b => !b.IsDeleted && !b.IsArchived)
                .OrderByDescending(b => b.StartDate)
                .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                .ToListAsync();

            vm.ProfessionalModels = await _context.ProfessionalModels
                .Where(m => !m.IsArchived)
                .OrderBy(m => m.Title)
                .Select(m => new SelectListItem
                {
                    Value = m.Id.ToString(),
                    Text = $"{m.Title} — {m.Description.Substring(0, Math.Min(50, m.Description.Length))}"
                })
                .ToListAsync();
        }
    }
}
