using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services.Admin;
using QdratNew.Services.Homework.Interfaces;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Reports;
using System.Security.Claims;
using QdratNew.Services.Implementations;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPermissionPolicies.Homework_Read)]
    public class HomeworkManagementController : Controller
    {
        private readonly IHomeworkManagementService _homeworkService;
        private readonly ApplicationDbContext _context;
        private readonly IHomeworkAnalyticsService _analyticsService;
        private readonly IAdvancedNotificationService _notificationService;
        private readonly ILessonCompletionService _lessonCompletionService;
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAdminActivityLogger _activityLogger;
        private readonly IEmployeeBatchAccessService _batchAccess;
        private readonly IHomeworkRecommendationService _homeworkRecommendationService;
        private readonly IBatchPerformanceRecommendationService _batchPerformanceRecommendationService;

        public HomeworkManagementController(
     ApplicationDbContext context,
     IHomeworkManagementService homeworkService,
     IAdvancedNotificationService notificationService,
     IDbContextFactory<ApplicationDbContext> contextFactory,
     ILessonCompletionService lessonCompletionService,
     IHomeworkAnalyticsService analyticsService,
     UserManager<ApplicationUser> userManager,
     IAdminActivityLogger activityLogger,
     IEmployeeBatchAccessService batchAccess,
     IHomeworkRecommendationService homeworkRecommendationService,
     IBatchPerformanceRecommendationService batchPerformanceRecommendationService
 )
        {
            _context = context;
            _homeworkService = homeworkService;
            _notificationService = notificationService;
            _contextFactory = contextFactory;
            _lessonCompletionService = lessonCompletionService;
            _analyticsService = analyticsService;
            _userManager = userManager;
            _activityLogger = activityLogger;
            _batchAccess = batchAccess;
            _homeworkRecommendationService = homeworkRecommendationService;
            _batchPerformanceRecommendationService = batchPerformanceRecommendationService;
        }

        private bool IsArchiveOwner()
        {
            return User.IsInRole("Owner") || User.IsInRole("Developer");
        }

        private string CurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }

        private string CurrentUserName()
        {
            return User.Identity?.Name ?? "غير معروف";
        }

        private async Task<bool> CanAccessArchivedHomeworkSetAsync(int homeworkSetId)
        {
            var archiveState = await _context.HomeworkSets
                .AsNoTracking()
                .Where(x => x.Id == homeworkSetId)
                .Select(x => new { x.IsArchived })
                .FirstOrDefaultAsync();

            if (archiveState == null)
                return false;

            if (!archiveState.IsArchived || IsArchiveOwner())
                return true;

            var userId = CurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            return await _context.HomeworkArchiveAccesses
                .AsNoTracking()
                .AnyAsync(x => x.HomeworkSetId == homeworkSetId &&
                               x.UserId == userId &&
                               x.IsActive);
        }

        private async Task<bool> CanAccessArchivedBatchAsync(int batchId)
        {
            var archivedHomeworkSetIds = await _context.HomeworkSets
                .AsNoTracking()
                .Where(x => x.BatchId == batchId && x.IsArchived)
                .Select(x => x.Id)
                .ToListAsync();

            if (!archivedHomeworkSetIds.Any())
                return true;

            if (IsArchiveOwner())
                return true;

            var userId = CurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            var allowedHomeworkSetIds = await _context.HomeworkArchiveAccesses
                .AsNoTracking()
                .Where(x => x.UserId == userId &&
                            x.IsActive)
                .Select(x => x.HomeworkSetId)
                .Distinct()
                .ToListAsync();

            var allowedSet = allowedHomeworkSetIds.ToHashSet();
            return archivedHomeworkSetIds.Any(allowedSet.Contains);
        }

        private IActionResult ArchivedAccessDenied()
        {
            TempData["Error"] = "هذه الواجبات داخل الأرشيف ولا يمكن الوصول إليها إلا للمالك أو المبرمج أو مستخدم لديه موافقة صريحة.";
            return RedirectToAction(nameof(Archived));
        }

        private async Task LogHomeworkArchiveActivityAsync(int homeworkSetId, string actionType, string description, int? batchId = null)
        {
            if (actionType is "ArchiveView" or "ArchiveEdit" or "ArchiveDelete")
            {
                var isArchived = await _context.HomeworkSets
                    .AsNoTracking()
                    .Where(x => x.Id == homeworkSetId)
                    .Select(x => x.IsArchived)
                    .FirstOrDefaultAsync();

                if (!isArchived)
                    return;
            }

            await _activityLogger.LogAsync(
                actionType,
                description,
                CurrentUserId(),
                CurrentUserName(),
                homeworkSetId,
                null,
                batchId);
        }

        private async Task<HashSet<int>?> GetAllowedArchivedHomeworkSetIdsAsync()
        {
            if (IsArchiveOwner())
                return null;

            var userId = CurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return new HashSet<int>();

            var ids = await _context.HomeworkArchiveAccesses
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.IsActive)
                .Select(x => x.HomeworkSetId)
                .ToListAsync();

            return ids.ToHashSet();
        }

        private async Task<List<HomeworkBatchCardVM>> BuildHomeworkBatchCardsAsync(bool archived)
        {
            var now = DateTime.Now;
            var allowedArchivedIds = archived ? await GetAllowedArchivedHomeworkSetIdsAsync() : null;

            // فلترة الدفعات بصلاحية Homework للموظفين/الأدمن
            List<int>? permittedBatchIds = null;
            if (!_batchAccess.IsPrivilegedUser(User))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
                permittedBatchIds = await _batchAccess.GetPermittedBatchIdsAsync(uid, InstructorBatchFeature.Homework);
            }

            var homeworkSets = await _context.HomeworkSets
                .AsNoTracking()
                .Where(hs => hs.IsArchived == archived &&
                             (permittedBatchIds == null || permittedBatchIds.Contains(hs.BatchId)))
                .Select(hs => new
                {
                    hs.Id,
                    hs.BatchId
                })
                .ToListAsync();

            if (archived && allowedArchivedIds != null)
            {
                if (!allowedArchivedIds.Any())
                    return new List<HomeworkBatchCardVM>();

                homeworkSets = homeworkSets
                    .Where(hs => allowedArchivedIds.Contains(hs.Id))
                    .ToList();
            }

            if (!homeworkSets.Any())
                return new List<HomeworkBatchCardVM>();

            var homeworkSetIdSet = homeworkSets.Select(x => x.Id).ToHashSet();

            var hwStats = homeworkSets
                .GroupBy(x => x.BatchId)
                .Select(g => new { BatchId = g.Key, Count = g.Count() })
                .ToList();

            var batchIdsWithHw = hwStats.Select(x => x.BatchId).ToHashSet();

            var allStudentStats = await _context.HomeworkSetStudents
                .AsNoTracking()
                .Select(hss => new
                {
                    hss.HomeworkSetId,
                    hss.StudentId,
                    hss.IsSubmitted,
                    hss.AssignedAt
                })
                .ToListAsync();

            var homeworkSetBatchMap = homeworkSets.ToDictionary(x => x.Id, x => x.BatchId);

            var studentStats = allStudentStats
                .Where(x => homeworkSetIdSet.Contains(x.HomeworkSetId))
                .Select(x => new
                {
                    BatchId = homeworkSetBatchMap[x.HomeworkSetId],
                    x.StudentId,
                    x.IsSubmitted,
                    x.AssignedAt
                })
                .ToList();

            var activeCounts = await (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join s in _context.Students.AsNoTracking() on e.StudentID equals s.StudentID
                join u in _context.Users.AsNoTracking() on s.UserId equals u.Id
                where u.IsActive
                group e by e.BatchId into g
                select new { BatchId = g.Key, Count = g.Count() }
            ).ToListAsync();

            var batches = await _context.Batches
                .AsNoTracking()
                .Where(b => !b.IsDeleted)
                .Include(b => b.Course)
                .OrderBy(b => b.Name)
                .ToListAsync();

            batches = batches
                .Where(b => batchIdsWithHw.Contains(b.Id))
                .ToList();

            return batches.Select(b =>
            {
                var hwCount = hwStats.FirstOrDefault(h => h.BatchId == b.Id)?.Count ?? 0;
                var bStats = studentStats.Where(s => s.BatchId == b.Id).ToList();
                var totalAssigned = bStats.Count;
                var totalSubmitted = bStats.Count(s => s.IsSubmitted);
                var solvedPct = totalAssigned > 0 ? (int)Math.Round(totalSubmitted * 100.0 / totalAssigned) : 0;
                var late24h = bStats
                    .Where(s => !s.IsSubmitted && (now - s.AssignedAt).TotalHours > 24 && (now - s.AssignedAt).TotalHours <= 72)
                    .Select(s => s.StudentId)
                    .Distinct()
                    .Count();
                var critical3d = bStats
                    .Where(s => !s.IsSubmitted && (now - s.AssignedAt).TotalHours > 72)
                    .Select(s => s.StudentId)
                    .Distinct()
                    .Count();

                return new HomeworkBatchCardVM
                {
                    BatchId = b.Id,
                    BatchName = b.Name,
                    CourseTitle = b.Course?.Name ?? "",
                    TotalStudents = activeCounts.FirstOrDefault(ac => ac.BatchId == b.Id)?.Count ?? 0,
                    TotalHomeworks = hwCount,
                    TotalAssigned = totalAssigned,
                    TotalSubmitted = totalSubmitted,
                    SolvedPercentage = solvedPct,
                    Late24hCount = late24h,
                    Critical3daysCount = critical3d
                };
            }).ToList();
        }


        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, int? batchId, bool showArchived = false)
        {
            var now = DateTime.Now;

            var hwStats = await _context.HomeworkSets
                .Where(hs => !hs.IsArchived)
                .GroupBy(hs => hs.BatchId)
                .Select(g => new { BatchId = g.Key, Count = g.Count() })
                .ToListAsync();

            var batchIdsWithHw = hwStats.Select(x => x.BatchId).ToHashSet();

            var studentStats = await (
                from hss in _context.HomeworkSetStudents
                join hs in _context.HomeworkSets on hss.HomeworkSetId equals hs.Id
                where !hs.IsArchived
                select new
                {
                    hs.BatchId,
                    hss.StudentId,
                    hss.IsSubmitted,
                    hss.AssignedAt
                }
            ).ToListAsync();

            var activeCounts = await (
                from e in _context.StudentBatchEnrollments
                join s in _context.Students on e.StudentID equals s.StudentID
                join u in _context.Users on s.UserId equals u.Id
                where u.IsActive
                group e by e.BatchId into g
                select new { BatchId = g.Key, Count = g.Count() }
            ).ToListAsync();

            var batches = await _context.Batches
                .Where(b => !b.IsDeleted && !b.IsArchived && batchIdsWithHw.Contains(b.Id))
                .Include(b => b.Course)
                .OrderBy(b => b.Name)
                .ToListAsync();

            var batchCards = batches.Select(b =>
            {
                var hwCount = hwStats.FirstOrDefault(h => h.BatchId == b.Id)?.Count ?? 0;
                var bStats = studentStats.Where(s => s.BatchId == b.Id).ToList();
                var totalAssigned = bStats.Count;
                var totalSubmitted = bStats.Count(s => s.IsSubmitted);
                var solvedPct = totalAssigned > 0 ? (int)Math.Round(totalSubmitted * 100.0 / totalAssigned) : 0;
                var late24h = bStats
                    .Where(s => !s.IsSubmitted && (now - s.AssignedAt).TotalHours > 24 && (now - s.AssignedAt).TotalHours <= 72)
                    .Select(s => s.StudentId).Distinct().Count();
                var critical3d = bStats
                    .Where(s => !s.IsSubmitted && (now - s.AssignedAt).TotalHours > 72)
                    .Select(s => s.StudentId).Distinct().Count();

                return new HomeworkBatchCardVM
                {
                    BatchId = b.Id,
                    BatchName = b.Name,
                    CourseTitle = b.Course?.Name ?? "",
                    TotalStudents = activeCounts.FirstOrDefault(ac => ac.BatchId == b.Id)?.Count ?? 0,
                    TotalHomeworks = hwCount,
                    TotalAssigned = totalAssigned,
                    TotalSubmitted = totalSubmitted,
                    SolvedPercentage = solvedPct,
                    Late24hCount = late24h,
                    Critical3daysCount = critical3d
                };
            }).ToList();

            var archivedHomeworks = await _homeworkService.GetHomeworksAsync(fromDate, toDate, batchId, true);
            if (!IsArchiveOwner())
            {
                var userId = CurrentUserId();
                var allowedArchivedIds = (await _context.HomeworkArchiveAccesses
                        .AsNoTracking()
                        .Where(x => x.UserId == userId && x.IsActive)
                        .Select(x => x.HomeworkSetId)
                        .ToListAsync())
                    .ToHashSet();

                archivedHomeworks = archivedHomeworks
                    .Where(x => allowedArchivedIds.Contains(x.HomeworkSetId))
                    .ToList();
            }

            var model = new HomeworkManagementIndexViewModel
            {
                Homeworks = await _homeworkService.GetHomeworksAsync(fromDate, toDate, batchId, false),
                ArchivedHomeworks = archivedHomeworks,
                Batches = _context.Batches
                    .OrderBy(b => b.Name)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                    .ToList(),
                FromDate = fromDate,
                ToDate = toDate,
                SelectedBatchId = batchId,
                ShowArchived = showArchived,
                BatchCards = batchCards,
                TotalBatches = batchCards.Count,
                TotalHomeworksCount = batchCards.Sum(x => x.TotalHomeworks),
                TotalLate24hStudents = batchCards.Sum(x => x.Late24hCount),
                TotalCriticalStudents = batchCards.Sum(x => x.Critical3daysCount)
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Archived()
        {
            var batchCards = await BuildHomeworkBatchCardsAsync(archived: true);
            var archivedHomeworks = await _homeworkService.GetHomeworksAsync(null, null, null, true);

            if (!IsArchiveOwner())
            {
                var allowedArchivedIds = await GetAllowedArchivedHomeworkSetIdsAsync() ?? new HashSet<int>();
                archivedHomeworks = archivedHomeworks
                    .Where(x => allowedArchivedIds.Contains(x.HomeworkSetId))
                    .ToList();
            }

            var model = new HomeworkManagementIndexViewModel
            {
                Homeworks = new List<HomeworkOverviewViewModel>(),
                ArchivedHomeworks = archivedHomeworks,
                Batches = new List<SelectListItem>(),
                ShowArchived = true,
                BatchCards = batchCards,
                TotalBatches = batchCards.Count,
                TotalHomeworksCount = batchCards.Sum(x => x.TotalHomeworks),
                TotalLate24hStudents = batchCards.Sum(x => x.Late24hCount),
                TotalCriticalStudents = batchCards.Sum(x => x.Critical3daysCount)
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> BatchHomeworks(int batchId, bool archived = false)
        {
            if (archived && !await CanAccessArchivedBatchAsync(batchId))
                return ArchivedAccessDenied();

            var now = DateTime.Now;
            var arCulture = new System.Globalization.CultureInfo("ar-SA");

            var batch = await _context.Batches
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null) return NotFound();

            // ── طلاب الدفعة ──────────────────────────────────────────
            var students = await (
                from e in _context.StudentBatchEnrollments
                join s in _context.Students on e.StudentID equals s.StudentID
                join u in _context.Users on s.UserId equals u.Id
                where e.BatchId == batchId && u.IsActive
                orderby s.FullName
                select new { s.StudentID, s.FullName, s.PhoneNumber }
            ).ToListAsync();

            var studentIds = students.Select(s => s.StudentID).ToList();

            // ── محاضرات الدفعة ───────────────────────────────────────
            var lectures = await _context.Lecture
                .Where(l => l.BatchId == batchId)
                .OrderBy(l => l.Date)
                .Select(l => new { l.Id, l.Title, l.Date })
                .ToListAsync();

            var lectureIds = lectures.Select(l => l.Id).ToList();

            // ── سجلات الحضور ─────────────────────────────────────────
            var attendanceRecs = await _context.AttendanceRecords
                .Where(a => lectureIds.Contains(a.LectureId) && studentIds.Contains(a.StudentId))
                .Select(a => new { a.StudentId, a.LectureId, a.IsPresent, a.IsLateArrival })
                .ToListAsync();

            // ── واجبات الدفعة ─────────────────────────────────────────
            var hwSetQuery = _context.HomeworkSets
                .AsNoTracking()
                .Where(hs => hs.BatchId == batchId && hs.IsArchived == archived);

            var hwSets = await hwSetQuery
                .OrderByDescending(hs => hs.CreatedAt)
                .Select(hs => new { hs.Id, hs.Title, hs.CompletionTitle, hs.CreatedAt, hs.EndAt, hs.IsClosed, hs.CurriculumId })
                .ToListAsync();

            if (archived && !IsArchiveOwner())
            {
                var allowedArchivedIds = await GetAllowedArchivedHomeworkSetIdsAsync() ?? new HashSet<int>();
                hwSets = hwSets
                    .Where(hs => allowedArchivedIds.Contains(hs.Id))
                    .ToList();

                if (!hwSets.Any())
                    return ArchivedAccessDenied();
            }

            var hwSetIds = hwSets.Select(hs => hs.Id).ToList();

            // ── مناهج دورة هذه الدفعة (Batch → Course → CourseCurriculums) ──
            // هذا هو المصدر الحقيقي لتحديد مناهج الدفعة
            var courseCurriculumIds = await _context.CourseCurriculums
                .Where(cc => cc.CourseId == batch.CourseId)
                .Select(cc => cc.CurriculumId)
                .ToListAsync();

            // ── استنتاج المنهج لكل واجب مقيّداً بمناهج الدورة ──────────

            // المستوى 1: HomeworkSetSections → Section.CurriculumId
            var hwSetSectionCurriculums = await (
                from hss in _context.HomeworkSetSections
                join sec in _context.Sections on hss.SectionId equals sec.Id
                where hwSetIds.Contains(hss.HomeworkSetId)
                      && sec.CurriculumId > 0
                      && (!courseCurriculumIds.Any() || courseCurriculumIds.Contains(sec.CurriculumId))
                select new { hss.HomeworkSetId, sec.CurriculumId }
            ).Distinct().ToListAsync();

            // المستوى 2: Homeworks → Questions → Lessons → Sections → CurriculumId
            var hwQuestionCurriculums = await (
                from hw in _context.Homeworks
                join q   in _context.Questions on hw.QuestionId equals q.Id
                join l   in _context.Lessons   on q.LessonId    equals l.Id
                join sec in _context.Sections  on l.SectionId   equals sec.Id
                where hwSetIds.Contains(hw.HomeworkSetId)
                      && sec.CurriculumId > 0
                      && (!courseCurriculumIds.Any() || courseCurriculumIds.Contains(sec.CurriculumId))
                select new { hw.HomeworkSetId, sec.CurriculumId }
            ).Distinct().ToListAsync();

            // بناء الخريطة: hwSetId → CurriculumId
            //   0) HomeworkSet.CurriculumId مباشرة — بلا فلتر (قيمة موثوقة)
            //   1) HomeworkSetSections (مُفلتَر بمناهج الدورة)
            //   2) Questions → Sections (مُفلتَر بمناهج الدورة)
            var hwCurriculumLookup = hwSets.ToDictionary(
                hs => hs.Id,
                hs =>
                {
                    if (hs.CurriculumId.HasValue) return (int?)hs.CurriculumId.Value;
                    var fromSec = hwSetSectionCurriculums.FirstOrDefault(x => x.HomeworkSetId == hs.Id);
                    if (fromSec != null) return (int?)fromSec.CurriculumId;
                    var fromQ   = hwQuestionCurriculums.FirstOrDefault(x => x.HomeworkSetId == hs.Id);
                    return fromQ != null ? (int?)fromQ.CurriculumId : null;
                }
            );

            // المناهج الفعلية المستخدمة في واجبات هذه الدفعة
            var curriculumIds = hwCurriculumLookup.Values
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .Distinct().ToList();

            var curriculumTitleMap = curriculumIds.Any()
                ? await _context.Curriculums
                    .Where(c => curriculumIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.Title)
                : new Dictionary<int, string>();

            // المدرب لكل منهج في هذه الدفعة (InstructorCurriculumBatches)
            var instructorMap = new Dictionary<int, string>();
            if (curriculumIds.Any())
            {
                var instructorRawList = await (
                    from icb in _context.InstructorCurriculumBatches
                    join ins in _context.Instructors on icb.InstructorId equals ins.Id
                    where icb.BatchId == batchId && curriculumIds.Contains(icb.CurriculumId)
                    select new { icb.CurriculumId, InstructorName = ins.FullName }
                ).ToListAsync();

                instructorMap = instructorRawList
                    .GroupBy(x => x.CurriculumId)
                    .ToDictionary(g => g.Key, g => g.First().InstructorName);
            }

            var curriculumInstructors = curriculumIds
                .Where(cid => curriculumTitleMap.ContainsKey(cid))
                .Select(cid => new CurriculumInstructorVM
                {
                    CurriculumId    = cid,
                    CurriculumTitle = curriculumTitleMap[cid],
                    InstructorName  = instructorMap.GetValueOrDefault(cid, "")
                }).ToList();

            // ── سجلات طلاب الواجبات ───────────────────────────────────
            var hwStudentRecs = await _context.HomeworkSetStudents
                .Where(r => hwSetIds.Contains(r.HomeworkSetId) && studentIds.Contains(r.StudentId))
                .Select(r => new
                {
                    r.HomeworkSetId, r.StudentId,
                    r.IsSubmitted, r.Score,
                    r.StudentReminderContacted, r.ParentContacted,
                    r.AssignedAt
                }).ToListAsync();

            // ── بناء كروت الطلاب ─────────────────────────────────────
            var studentCards = students.Select(s =>
            {
                var sAtt = attendanceRecs.Where(a => a.StudentId == s.StudentID).ToList();

                var lectureVMs = lectures.Select(l =>
                {
                    var rec = sAtt.FirstOrDefault(a => a.LectureId == l.Id);
                    return new StudentLectureVM
                    {
                        Title      = l.Title,
                        Date       = l.Date.ToString("yyyy/MM/dd"),
                        MonthLabel = l.Date.ToString("MMMM yyyy", arCulture),
                        HasRecord  = rec != null,
                        IsPresent  = rec?.IsPresent ?? false,
                        IsLate     = rec?.IsLateArrival ?? false
                    };
                }).ToList();

                var hwVMs = hwSets.Select(hs =>
                {
                    var rec = hwStudentRecs.FirstOrDefault(r => r.HomeworkSetId == hs.Id && r.StudentId == s.StudentID);
                    hwCurriculumLookup.TryGetValue(hs.Id, out var effCid);
                    return new StudentHwVM
                    {
                        Title            = !string.IsNullOrEmpty(hs.CompletionTitle) ? hs.CompletionTitle : hs.Title,
                        Date             = hs.CreatedAt.ToString("yyyy/MM/dd"),
                        IsSolved         = rec?.IsSubmitted ?? false,
                        Score            = rec?.Score,
                        StudentContacted = rec?.StudentReminderContacted ?? false,
                        ParentContacted  = rec?.ParentContacted ?? false,
                        CurriculumId     = effCid,
                        CurriculumTitle  = effCid.HasValue ? curriculumTitleMap.GetValueOrDefault(effCid.Value) : null
                    };
                }).ToList();

                return new BatchStudentCardVM
                {
                    StudentId      = s.StudentID,
                    FullName       = s.FullName,
                    PhoneNumber    = s.PhoneNumber,
                    AttendedCount  = lectureVMs.Count(l => l.HasRecord && l.IsPresent),
                    TotalLectures  = lectures.Count,
                    SolvedHomeworks = hwVMs.Count(h => h.IsSolved),
                    TotalHomeworks = hwSets.Count,
                    Lectures       = lectureVMs,
                    Homeworks      = hwVMs
                };
            }).ToList();

            // ── بناء كروت الواجبات ────────────────────────────────────
            var homeworkCards = hwSets.Select(hs =>
            {
                var stats = hwStudentRecs.Where(r => r.HomeworkSetId == hs.Id).ToList();
                var totalAssigned = stats.Count;
                var submitted     = stats.Count(r => r.IsSubmitted);
                var solvedPct     = totalAssigned > 0 ? (int)Math.Round(submitted * 100.0 / totalAssigned) : 0;
                var late24h = stats
                    .Where(r => !r.IsSubmitted && (now - r.AssignedAt).TotalHours > 24 && (now - r.AssignedAt).TotalHours <= 72)
                    .Select(r => r.StudentId).Distinct().Count();
                var critical3d = stats
                    .Where(r => !r.IsSubmitted && (now - r.AssignedAt).TotalHours > 72)
                    .Select(r => r.StudentId).Distinct().Count();

                return new HomeworkCardForBatchVM
                {
                    HomeworkSetId    = hs.Id,
                    Title            = !string.IsNullOrEmpty(hs.CompletionTitle) ? hs.CompletionTitle : hs.Title,
                    CreatedAt        = hs.CreatedAt,
                    EndAt            = hs.EndAt,
                    TotalAssigned    = totalAssigned,
                    SubmittedCount   = submitted,
                    SolvedPercentage = solvedPct,
                    Late24hCount     = late24h,
                    Critical3daysCount = critical3d,
                    IsClosed         = hs.IsClosed
                };
            }).ToList();

            var vm = new HomeworkBatchDetailsPageVM
            {
                BatchId               = batchId,
                BatchName             = batch.Name,
                CourseTitle           = batch.Course?.Name ?? "",
                TotalStudents         = students.Count,
                Homeworks             = homeworkCards,
                Students              = studentCards,
                CurriculumInstructors = curriculumInstructors
            };

            ViewBag.IsArchived = archived;

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("HomeworkManagement", "Archive")]
        public async Task<IActionResult> ArchiveHomeworkSetsByBatches(List<int> selectedBatchIds)
        {
            if (selectedBatchIds == null || !selectedBatchIds.Any())
            {
                TempData["Error"] = "⚠️ اختر دفعة واحدة على الأقل للأرشفة.";
                return RedirectToAction(nameof(Index));
            }

            var batchIds = selectedBatchIds.Where(x => x > 0).Distinct().ToList();
            if (!batchIds.Any())
            {
                TempData["Error"] = "⚠️ لم يتم العثور على دفعات صالحة للأرشفة.";
                return RedirectToAction(nameof(Index));
            }

            var allHomeworks = await _context.HomeworkSets
                .Where(hs => !hs.IsArchived)
                .ToListAsync();

            var homeworks = allHomeworks
                .Where(hs => batchIds.Any(batchId => batchId == hs.BatchId))
                .ToList();

            var archivedAt = DateTime.UtcNow;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            foreach (var homework in homeworks)
            {
                homework.IsArchived = true;
                homework.ArchivedAt = archivedAt;
                homework.ArchivedByUserId = userId;
            }

            await _context.SaveChangesAsync();

            foreach (var homework in homeworks)
            {
                await LogHomeworkArchiveActivityAsync(
                    homework.Id,
                    "Archive",
                    $"تمت أرشفة الواجب '{homework.Title}' ضمن أرشفة دفعات جماعية.",
                    homework.BatchId);
            }

            TempData["Success"] = $"✅ تم أرشفة {homeworks.Count} واجب.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("HomeworkManagement", "Archive")]
        public async Task<IActionResult> RestoreHomeworkSetsByBatches(List<int> selectedBatchIds)
        {
            if (selectedBatchIds == null || !selectedBatchIds.Any())
            {
                TempData["Error"] = "⚠️ اختر دفعة واحدة على الأقل للاسترجاع.";
                return RedirectToAction(nameof(Index));
            }

            var batchIds = selectedBatchIds.Where(x => x > 0).Distinct().ToList();
            if (!batchIds.Any())
            {
                TempData["Error"] = "⚠️ لم يتم العثور على دفعات صالحة للاسترجاع.";
                return RedirectToAction(nameof(Index));
            }

            var allHomeworks = await _context.HomeworkSets
                .Where(hs => hs.IsArchived)
                .ToListAsync();

            var homeworks = allHomeworks
                .Where(hs => batchIds.Any(batchId => batchId == hs.BatchId))
                .ToList();

            foreach (var homework in homeworks)
            {
                homework.IsArchived = false;
                homework.ArchivedAt = null;
                homework.ArchivedByUserId = null;
            }

            await _context.SaveChangesAsync();

            foreach (var homework in homeworks)
            {
                await LogHomeworkArchiveActivityAsync(
                    homework.Id,
                    "Restore",
                    $"تم إخراج الواجب '{homework.Title}' من الأرشيف ضمن استرجاع دفعات جماعي.",
                    homework.BatchId);
            }

            TempData["Success"] = $"✅ تم إخراج {homeworks.Count} واجب من الأرشيف.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("HomeworkManagement", "Archive")]
        public async Task<IActionResult> ArchiveHomeworkSet(int id)
        {
            var set = await _context.HomeworkSets.FirstOrDefaultAsync(x => x.Id == id);

            if (set == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على الواجب.";
                return RedirectToAction(nameof(Index));
            }

            set.IsArchived = true;
            set.ArchivedAt = DateTime.UtcNow;
            set.ArchivedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            await _context.SaveChangesAsync();

            await LogHomeworkArchiveActivityAsync(
                set.Id,
                "Archive",
                $"تم نقل الواجب '{set.Title}' إلى الأرشيف.",
                set.BatchId);

            TempData["Success"] = "✅ تم نقل الواجب إلى الأرشيف.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("HomeworkManagement", "Archive")]
        public async Task<IActionResult> RestoreHomeworkSet(int id)
        {
            var set = await _context.HomeworkSets.FirstOrDefaultAsync(x => x.Id == id);

            if (set == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على الواجب.";
                return RedirectToAction(nameof(Index), new { showArchived = true });
            }

            set.IsArchived = false;
            set.ArchivedAt = null;
            set.ArchivedByUserId = null;

            await _context.SaveChangesAsync();

            await LogHomeworkArchiveActivityAsync(
                set.Id,
                "Restore",
                $"تم إخراج الواجب '{set.Title}' من الأرشيف.",
                set.BatchId);

            TempData["Success"] = "✅ تم إخراج الواجب من الأرشيف.";
            return RedirectToAction(nameof(Index), new { showArchived = true });
        }


        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(id))
                return ArchivedAccessDenied();

            var homeworkSet = await _context.HomeworkSets
                .AsNoTracking()
                .Include(h => h.Batch)
                .Include(h => h.Curriculum)
                .FirstOrDefaultAsync(h => h.Id == id);

            if (homeworkSet == null)
                return NotFound();

            if (homeworkSet.IsArchived)
            {
                await LogHomeworkArchiveActivityAsync(
                    homeworkSet.Id,
                    "ArchiveView",
                    $"تم فتح تفاصيل الواجب المؤرشف '{homeworkSet.Title}'.",
                    homeworkSet.BatchId);
            }

            var homeworkRows = await _context.Homeworks
                .AsNoTracking()
                .Include(h => h.Question)
                    .ThenInclude(q => q.Curriculum)
                .Include(h => h.Question)
                    .ThenInclude(q => q.Section)
                .Include(h => h.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                .Where(h => h.HomeworkSetId == id)
                .OrderBy(h => h.Id)
                .ToListAsync();

            var questions = homeworkRows
                .Where(h => h.Question != null)
                .GroupBy(h => h.QuestionId)
                .Select((group, index) =>
                {
                    var question = group.First().Question;
                    var sectionTitle = question.Lesson?.Section?.Title
                        ?? question.Section?.Title
                        ?? "—";

                    return new HomeworkQuestionDetailsItemViewModel
                    {
                        QuestionId = question.Id,
                        Order = index + 1,
                        Title = question.Title ?? "",
                        CurriculumTitle = question.Curriculum?.Title ?? homeworkSet.Curriculum?.Title ?? "—",
                        SectionTitle = sectionTitle,
                        LessonTitle = question.Lesson?.Title ?? "—",
                        DifficultyLevel = (int)question.Difficulty,
                        DifficultyText = GetDifficultyText(question.Difficulty),
                        CorrectAnswer = question.CorrectAnswer ?? "",
                        ReferenceNumber = question.ReferenceNumber ?? ""
                    };
                })
                .ToList();

            var model = new HomeworkSetDetailsViewModel
            {
                HomeworkSetId = homeworkSet.Id,
                Title = homeworkSet.Title,
                BatchName = homeworkSet.Batch?.Name ?? "—",
                CurriculumTitle = homeworkSet.Curriculum?.Title ?? "—",
                CompletionTitle = homeworkSet.CompletionTitle ?? "",
                CreatedAt = homeworkSet.CreatedAt,
                StartAt = homeworkSet.StartAt,
                EndAt = homeworkSet.EndAt,
                IsSent = homeworkSet.IsSent || homeworkRows.Any(h => h.IsSent),
                IsClosed = homeworkSet.IsClosed,
                IsExtra = homeworkSet.IsExtra,
                AllowRetake = homeworkSet.AllowRetake,
                MaxRetakes = homeworkSet.MaxRetakes,
                TotalQuestions = questions.Count,
                TotalSections = questions
                    .Where(q => !string.IsNullOrWhiteSpace(q.SectionTitle) && q.SectionTitle != "—")
                    .Select(q => q.SectionTitle)
                    .Distinct()
                    .Count(),
                TotalLessons = questions
                    .Where(q => !string.IsNullOrWhiteSpace(q.LessonTitle) && q.LessonTitle != "—")
                    .Select(q => q.LessonTitle)
                    .Distinct()
                    .Count(),
                AssignedStudentsCount = homeworkRows
                    .Select(h => h.StudentId)
                    .Distinct()
                    .Count(),
                Questions = questions
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> StudentsReport(int homeworkSetId)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return ArchivedAccessDenied();

            var details = await _homeworkService.GetHomeworkDetailsAsync(homeworkSetId);

            if (details == null)
                return NotFound();

            await LogHomeworkArchiveActivityAsync(
                homeworkSetId,
                "ArchiveView",
                $"تم فتح تقرير طلاب واجب مؤرشف لدفعة '{details.BatchName}'.",
                details.BatchId);

            return View(details);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStudentFollowUp(
            int homeworkSetId,
            int studentId,
            bool studentReminderContacted,
            bool parentContacted)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();

            var followUp = await _context.HomeworkSetStudents
                .FirstOrDefaultAsync(x => x.HomeworkSetId == homeworkSetId && x.StudentId == studentId);

            if (followUp == null)
                return NotFound(new { success = false, message = "لم يتم العثور على ربط الطالب بهذا الواجب." });

            if (followUp.IsSubmitted)
                return BadRequest(new { success = false, message = "لا يمكن تحديث متابعة واجب تم تسليمه بالفعل." });

            var now = DateTime.Now;
            var shouldSendStudentReminder = studentReminderContacted && !followUp.StudentReminderContacted;

            followUp.StudentReminderContacted = studentReminderContacted;
            followUp.StudentReminderContactedAt = studentReminderContacted
                ? followUp.StudentReminderContactedAt ?? now
                : null;

            followUp.ParentContacted = parentContacted;
            followUp.ParentContactedAt = parentContacted
                ? followUp.ParentContactedAt ?? now
                : null;

            followUp.LastUpdated = now;

            await _context.SaveChangesAsync();

            await LogHomeworkArchiveActivityAsync(
                homeworkSetId,
                "ArchiveEdit",
                $"تم تعديل متابعة الطالب رقم {studentId} داخل واجب مؤرشف.");

            if (shouldSendStudentReminder)
            {
                var targetUrl = Url.Action(
                    "Index",
                    "StudentHomeworkDashboard",
                    new { area = "Students" });

                await _notificationService.SendToStudentAsync(
                    studentId,
                    "تذكير: يوجد واجب لم يتم حله بعد. يرجى الدخول وحل الواجب في أقرب وقت.",
                    NotificationCategory.Reminder,
                    targetUrl);
            }

            return Json(new
            {
                success = true,
                studentReminderContacted = followUp.StudentReminderContacted,
                parentContacted = followUp.ParentContacted,
                studentReminderContactedAt = followUp.StudentReminderContactedAt?.ToString("yyyy-MM-dd HH:mm"),
                parentContactedAt = followUp.ParentContactedAt?.ToString("yyyy-MM-dd HH:mm")
            });
        }

        private static string GetDifficultyText(DifficultyLevel difficulty)
        {
            return difficulty switch
            {
                DifficultyLevel.Easy => "سهل",
                DifficultyLevel.Medium => "متوسط",
                DifficultyLevel.Hard => "صعب",
                DifficultyLevel.VeryHard => "صعب جدًا",
                _ => "—"
            };
        }


        public async Task<IActionResult> Review(int studentId, int homeworkSetId)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return ArchivedAccessDenied();

            var student = await _context.Students.FindAsync(studentId);
            if (student == null) return NotFound();

            var questions = await (from hw in _context.Homeworks
                                   join q in _context.Questions on hw.QuestionId equals q.Id
                                   where hw.StudentId == studentId && hw.HomeworkSetId == homeworkSetId
                                   select new ReviewedHomeworkQuestionViewModel
                                   {
                                       HomeworkId = hw.Id,
                                       QuestionId = q.Id,
                                       QuestionText = q.Title,
                                       Answer = hw.StudentAnswer,
                                       IsCorrect = hw.IsCorrect
                                   }).ToListAsync();

            var model = new StudentHomeworkReviewViewModel
            {
                StudentId = studentId,
                StudentName = student.FullName,
                HomeworkSetId = homeworkSetId,
                Questions = questions
            };

            await LogHomeworkArchiveActivityAsync(
                homeworkSetId,
                "ArchiveView",
                $"تم فتح مراجعة واجب مؤرشف للطالب رقم {studentId}.");

            return View(model);
        }

        [HttpGet]
        //[AdminPermission("HomeworkManagement", "Review")]
        public async Task<IActionResult> PreviewQuestion(
       Guid id,
       int studentId,
       int homeworkSetId)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();

            // ===============================
            // 1) جلب السؤال
            // ===============================
            var question = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
                return NotFound("السؤال غير موجود.");

            var model = question.ToDisplayModel();

            // ===============================
            // 2) جلب إجابة الطالب الصحيحة لهذا الواجب
            // ===============================
            var homeworkAnswer = await _context.Homeworks
                .Where(h =>
                    h.QuestionId == id &&
                    h.StudentId == studentId &&
                    h.HomeworkSetId == homeworkSetId)
                .OrderByDescending(h => h.Id)
                .Select(h => h.StudentAnswer)
                .FirstOrDefaultAsync();

            // ===============================
            // 3) تمرير الإجابة (إن وُجدت)
            // ===============================
            ViewBag.SelectedAnswer = homeworkAnswer ?? "";

            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> PreviewHomeworkQuestion(Guid id)
        {
            var question = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
                return NotFound("السؤال غير موجود.");

            ViewBag.SelectedAnswer = "";

            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", question.ToDisplayModel());
        }



        [HttpGet]
        [AdminPermission("HomeworkManagement", "ManageQuestions")]
        public async Task<IActionResult> ManageHomeworkQuestions(int homeworkSetId, string searchTerm = "")
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return ArchivedAccessDenied();

            // 🟢 جلب الواجب المحدد
            var set = await _context.HomeworkSets
                .Include(h => h.Batch)
                .FirstOrDefaultAsync(h => h.Id == homeworkSetId);

            if (set == null)
                return NotFound("❌ لم يتم العثور على الواجب المطلوب.");

            // 🟡 جلب الأسئلة الحالية في الواجب بدون تكرار لكل طالب
            var existingQuestions = await (
                from hw in _context.Homeworks.AsNoTracking()
                join q in _context.Questions.AsNoTracking() on hw.QuestionId equals q.Id
                join l in _context.Lessons.AsNoTracking() on q.LessonId equals l.Id
                where hw.HomeworkSetId == homeworkSetId
                group new { q, l } by new
                {
                    q.Id,
                    q.Title,
                    q.InternalNote,
                    LessonTitle = l.Title,
                    q.IsReviewed,
                    q.ImageUrl
                }
                into g
                select new HomeworkQuestionManageItem
                {
                    QuestionId = g.Key.Id,
                    Title = g.Key.Title,
                    InternalNote = g.Key.InternalNote,
                    LessonTitle = g.Key.LessonTitle,
                    IsReviewed = g.Key.IsReviewed,
                    HasImage = g.Key.ImageUrl != null
                }).ToListAsync();

            // 🔍 في حالة البحث — نجلب أيضًا الأسئلة الأخرى غير المرتبطة بنفس الواجب
            List<HomeworkQuestionManageItem> searchResults = new();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchResults = await _context.Questions
       .Where(q =>
           (!string.IsNullOrEmpty(q.InternalNote) && EF.Functions.Like(q.InternalNote, "%" + searchTerm + "%")) ||
           (!string.IsNullOrEmpty(q.Title) && EF.Functions.Like(q.Title, "%" + searchTerm + "%"))
       )
       .Select(q => new HomeworkQuestionManageItem
       {
           QuestionId = q.Id,
           Title = q.Title,
           InternalNote = q.InternalNote,
           LessonTitle = q.Lesson.Title,
           IsReviewed = q.IsReviewed,
           HasImage = q.ImageUrl != null
       })
       .Take(50)
       .ToListAsync();

            }

            var model = new ManageHomeworkQuestionsViewModel
            {
                HomeworkSetId = homeworkSetId,
                HomeworkTitle = set.Title,
                ExistingQuestions = existingQuestions,
                SearchResults = searchResults,
                SearchTerm = searchTerm
            };

            if (set.IsArchived)
            {
                await LogHomeworkArchiveActivityAsync(
                    homeworkSetId,
                    "ArchiveView",
                    $"تم فتح إدارة أسئلة الواجب المؤرشف '{set.Title}'.",
                    set.BatchId);
            }

            return View(model);
        }


        [HttpPost]
        [AdminPermission("HomeworkManagement", "AddQuestion")]
        public async Task<IActionResult> AddQuestionToHomework(int homeworkSetId, Guid questionId)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return ArchivedAccessDenied();

            var question = await _context.Questions
                .Include(q => q.Lesson)
                .FirstOrDefaultAsync(q => q.Id == questionId);
            if (question == null)
                return NotFound("السؤال غير موجود.");

            var students = await _context.HomeworkSetStudents
                .Where(s => s.HomeworkSetId == homeworkSetId)
                .Select(s => s.StudentId)
                .ToListAsync();

            var existingStudentIds = await _context.Homeworks
                .Where(h => h.HomeworkSetId == homeworkSetId && h.QuestionId == questionId)
                .Select(h => h.StudentId)
                .ToListAsync();

            var newHomeworks = students
                .Where(sid => !existingStudentIds.Contains(sid))
                .Select(sid => new Homework
            {
                StudentId = sid,
                QuestionId = questionId,
                LessonId = question.LessonId,
                HomeworkSetId = homeworkSetId,
                AssignedAt = DateTime.Now,
                IsSent = true,
                Status = HomeworkStatus.Pending
            }).ToList();

            if (newHomeworks.Any())
                _context.Homeworks.AddRange(newHomeworks);

            if (question.Lesson?.SectionId > 0)
            {
                var sectionExists = await _context.HomeworkSetSections
                    .AnyAsync(x => x.HomeworkSetId == homeworkSetId && x.SectionId == question.Lesson.SectionId);

                if (!sectionExists)
                {
                    _context.HomeworkSetSections.Add(new HomeworkSetSection
                    {
                        HomeworkSetId = homeworkSetId,
                        SectionId = question.Lesson.SectionId
                    });
                }
            }

            await _context.SaveChangesAsync();

            await LogHomeworkArchiveActivityAsync(
                homeworkSetId,
                "ArchiveEdit",
                $"تمت إضافة السؤال {questionId} إلى واجب مؤرشف.");

            TempData["SuccessMessage"] = "✅ تم إضافة السؤال للواجب بنجاح.";
            return RedirectToAction(nameof(ManageHomeworkQuestions), new { homeworkSetId });
        }




        [HttpPost]
        [AdminPermission("HomeworkManagement", "AddQuestion")]
        public async Task<IActionResult> AddMultipleQuestionsToHomework([FromBody] AddQuestionsRequest request)
        {
            if (request?.QuestionIds == null || !request.QuestionIds.Any())
                return BadRequest("لم يتم اختيار أي أسئلة.");

            if (!await CanAccessArchivedHomeworkSetAsync(request.HomeworkSetId))
                return Forbid();

            var homeworkSet = await _context.HomeworkSets.FindAsync(request.HomeworkSetId);
            if (homeworkSet == null)
                return NotFound("الواجب غير موجود.");

            var studentIds = await _context.HomeworkSetStudents
                .Where(s => s.HomeworkSetId == request.HomeworkSetId)
                .Select(s => s.StudentId)
                .ToListAsync();

            if (!studentIds.Any())
                return BadRequest("لا يوجد طلاب مرتبطين بهذا الواجب.");

            int addedQuestions = 0;
            int addedRows = 0;
            foreach (var q in request.QuestionIds)
            {
                if (!Guid.TryParse(q, out var qid)) continue;

                var question = await _context.Questions
                    .Include(x => x.Lesson)
                    .FirstOrDefaultAsync(x => x.Id == qid);

                if (question == null) continue;

                var existingStudentIds = await _context.Homeworks
                    .Where(h => h.HomeworkSetId == request.HomeworkSetId && h.QuestionId == qid)
                    .Select(h => h.StudentId)
                    .ToListAsync();

                var newHomeworks = studentIds
                    .Where(sid => !existingStudentIds.Contains(sid))
                    .Select(sid => new Homework
                {
                    StudentId = sid,
                    HomeworkSetId = request.HomeworkSetId,
                    QuestionId = qid,
                    LessonId = question.LessonId,
                    AssignedAt = DateTime.Now,
                    IsSent = true,
                    Status = HomeworkStatus.Pending
                }).ToList();

                if (!newHomeworks.Any()) continue;

                _context.Homeworks.AddRange(newHomeworks);
                addedQuestions++;
                addedRows += newHomeworks.Count;

                if (question.Lesson?.SectionId > 0)
                {
                    var sectionExists = await _context.HomeworkSetSections
                        .AnyAsync(x => x.HomeworkSetId == request.HomeworkSetId && x.SectionId == question.Lesson.SectionId);

                    if (!sectionExists)
                    {
                        _context.HomeworkSetSections.Add(new HomeworkSetSection
                        {
                            HomeworkSetId = request.HomeworkSetId,
                            SectionId = question.Lesson.SectionId
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            await LogHomeworkArchiveActivityAsync(
                request.HomeworkSetId,
                "ArchiveEdit",
                $"تمت إضافة {addedQuestions} سؤال إلى واجب مؤرشف.");

            return Ok(new { message = $"تمت إضافة {addedQuestions} سؤال إلى الواجب، بعدد {addedRows} سجل للطلاب." });
        }

        public class AddQuestionsRequest
        {
            public int HomeworkSetId { get; set; }
            public List<string> QuestionIds { get; set; }
        }






        [HttpPost]
        [AdminPermission("HomeworkManagement", "RemoveQuestion")]
        public async Task<IActionResult> RemoveQuestionFromHomework(int homeworkSetId, Guid questionId)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return ArchivedAccessDenied();

            var existing = await _context.Homeworks
                .Where(h => h.HomeworkSetId == homeworkSetId && h.QuestionId == questionId)
                .ToListAsync();

            if (existing.Any())
            {
                _context.Homeworks.RemoveRange(existing);
                await _context.SaveChangesAsync();
                await LogHomeworkArchiveActivityAsync(
                    homeworkSetId,
                    "ArchiveEdit",
                    $"تم حذف السؤال {questionId} من واجب مؤرشف.");
                TempData["SuccessMessage"] = "🗑️ تم حذف السؤال من الواجب.";
            }

            return RedirectToAction(nameof(ManageHomeworkQuestions), new { homeworkSetId });
        }




        [HttpPost]
        [IgnoreAntiforgeryToken]
        [AdminPermission("HomeworkManagement", "ManageQuestions")]
        public async Task<IActionResult> LoadQuestionsData(
            int homeworkSetId,
            string searchTerm,
            int draw = 0,
            int start = 0,
            int length = 10)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
            {
                return Json(new
                {
                    draw,
                    data = new List<object>(),
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    error = "لا تملك صلاحية الوصول إلى هذا الواجب المؤرشف."
                });
            }

            try
            {
                var homeworkSet = await _context.HomeworkSets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.Id == homeworkSetId);

                if (homeworkSet == null)
                {
                    return Json(new
                    {
                        draw,
                        data = new List<object>(),
                        recordsTotal = 0,
                        recordsFiltered = 0,
                        error = "الواجب غير موجود."
                    });
                }

                // 🟢 المحاور المرتبطة رسميًا بالواجب
                var sectionIds = await _context.HomeworkSetSections
                    .AsNoTracking()
                    .Where(s => s.HomeworkSetId == homeworkSetId)
                    .Select(s => s.SectionId)
                    .Distinct()
                    .ToListAsync();

                // 🟡 fallback: استنتاج المحاور من الأسئلة الموجودة بالفعل في الواجب
                if (!sectionIds.Any())
                {
                    sectionIds = await (
                        from hw in _context.Homeworks.AsNoTracking()
                        join q in _context.Questions.AsNoTracking() on hw.QuestionId equals q.Id
                        join l in _context.Lessons.AsNoTracking() on q.LessonId equals l.Id
                        where hw.HomeworkSetId == homeworkSetId
                        select l.SectionId
                    )
                    .Distinct()
                    .ToListAsync();
                }

                var existingQuestionIds = await _context.Homeworks
                    .AsNoTracking()
                    .Where(h => h.HomeworkSetId == homeworkSetId)
                    .Select(h => h.QuestionId)
                    .Distinct()
                    .ToListAsync();

                // 🟢 الاستعلام الرئيسي: أسئلة مراجَعة وصالحة للإضافة
                var query =
                    from q in _context.Questions.AsNoTracking()
                    join l in _context.Lessons.AsNoTracking() on q.LessonId equals l.Id
                    join s in _context.Sections.AsNoTracking() on l.SectionId equals s.Id
                    where q.IsReviewed && !q.IsRejected
                    select new
                    {
                        q.Id,
                        q.Title,
                        q.CurriculumId,
                        SectionId = s.Id,
                        LessonTitle = l.Title,
                        SectionTitle = s.Title,
                        q.InternalNote,
                        q.IsAnswerConfirmed,
                        q.CreatedAt
                    };

                if (sectionIds.Any())
                {
                    query = query.Where(q => sectionIds.Contains(q.SectionId));
                }
                else if (homeworkSet.CurriculumId.HasValue)
                {
                    query = query.Where(q => q.CurriculumId == homeworkSet.CurriculumId.Value);
                }

                if (existingQuestionIds.Any())
                {
                    query = query.Where(q => !existingQuestionIds.Contains(q.Id));
                }

                var recordsTotal = await query.CountAsync();

                // 🟡 فلترة البحث النصي باستخدام LIKE بدل Contains
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query =
                        from q in query
                        where
                            (q.Title != null && EF.Functions.Like(q.Title, "%" + searchTerm + "%")) ||
                            (q.InternalNote != null && EF.Functions.Like(q.InternalNote, "%" + searchTerm + "%"))
                        select q;
                }

                var recordsFiltered = await query.CountAsync();

                // 🟢 جلب البيانات النهائية
                var pagedQuery = query
                    .OrderByDescending(q => q.CreatedAt)
                    .Skip(start);

                if (length > 0)
                    pagedQuery = pagedQuery.Take(length);

                var data = await pagedQuery
                    .Select(q => new
                    {
                        questionId = q.Id,
                        title = q.Title ?? "",
                        sectionTitle = q.SectionTitle ?? "",
                        lessonTitle = q.LessonTitle ?? "",
                        internalNote = q.InternalNote ?? "",
                        isReviewed = q.IsAnswerConfirmed,
                        createdAt = q.CreatedAt.ToString("yyyy/MM/dd")
                    })
                    .ToListAsync();

                return Json(new
                {
                    draw,
                    data,
                    recordsTotal,
                    recordsFiltered
                });
            }
            catch (Exception ex)
            {
                return Json(new { draw, error = ex.Message, data = new List<object>(), recordsTotal = 0, recordsFiltered = 0 });
            }
        }










        [HttpPost]
        [AdminPermission("HomeworkManagement", "Resend")]
        public async Task<IActionResult> ResendToBatch(int homeworkSetId)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return ArchivedAccessDenied();

            // ======================================
            // 1️⃣ جلب الواجب
            // ======================================
            var set = await _context.HomeworkSets
                .Include(hs => hs.Batch)
                .FirstOrDefaultAsync(hs => hs.Id == homeworkSetId);

            if (set == null)
                return NotFound("❌ لم يتم العثور على الواجب المحدد.");

            // ======================================
            // 2️⃣ جلب طلاب الدفعة
            // ======================================
            var allBatchStudentIds = await _context.StudentBatchEnrollments
                .Where(e => e.BatchId == set.BatchId)
                .Select(e => e.StudentID)
                .ToListAsync();

            if (!allBatchStudentIds.Any())
                return BadRequest("⚠️ لا يوجد طلاب في هذه الدفعة.");

            // ======================================
            // 3️⃣ الطلاب الموجودين بالفعل
            // ======================================
            var existingStudentIds = await _context.Homeworks
                .Where(h => h.HomeworkSetId == homeworkSetId)
                .Select(h => h.StudentId)
                .Distinct()
                .ToListAsync();

            // ======================================
            // 4️⃣ تحديد الطلاب الجدد (بدون Contains)
            // ======================================
            var newStudentIds = (
                from sid in allBatchStudentIds
                join eid in existingStudentIds on sid equals eid into gj
                from sub in gj.DefaultIfEmpty()
                where sub == null
                select sid
            ).ToList();

            // ======================================
            // 5️⃣ جلب Sections
            // ======================================
            var sectionIds = await _context.HomeworkSetSections
                .Where(s => s.HomeworkSetId == homeworkSetId)
                .Select(s => s.SectionId)
                .ToListAsync();

            // ======================================
            // 🔥 FIX: fallback من الأسئلة الموجودة
            // ======================================
            if (!sectionIds.Any())
            {
                sectionIds = await (
                    from hw in _context.Homeworks
                    join q in _context.Questions on hw.QuestionId equals q.Id
                    join l in _context.Lessons on q.LessonId equals l.Id
                    where hw.HomeworkSetId == homeworkSetId
                    select l.SectionId
                )
                .Distinct()
                .ToListAsync();
            }

            if (!sectionIds.Any())
                return BadRequest("⚠️ لا توجد محاور (Sections) مرتبطة بهذا الواجب.");

            // ======================================
            // 6️⃣ هل يوجد أسئلة
            // ======================================
            var hasQuestions = await _context.Homeworks
                .AnyAsync(h => h.HomeworkSetId == homeworkSetId);

            bool generated = false;

            // ======================================
            // 7️⃣ التوليد
            // ======================================
            if (!hasQuestions)
            {
                generated = await _lessonCompletionService.GenerateHomeworksForExtraSetAsync(
                    homeworkSetId,
                    sectionIds,
                    allBatchStudentIds,
                    10
                );
            }
            else if (newStudentIds.Any())
            {
                generated = await _lessonCompletionService.GenerateHomeworksForExtraSetAsync(
                    homeworkSetId,
                    sectionIds,
                    newStudentIds,
                    10
                );
            }

            // ======================================
            // 8️⃣ تحديث الحالة
            // ======================================
            set.IsSent = true;
            await _context.SaveChangesAsync();

            await LogHomeworkArchiveActivityAsync(
                homeworkSetId,
                "ArchiveEdit",
                $"تمت إعادة إرسال الواجب المؤرشف '{set.Title}' للدفعة.");

            // ======================================
            // 9️⃣ إشعارات
            // ======================================
            await _notificationService.SendToStudentsAsync(
                allBatchStudentIds,
                "📢 تم إعادة إرسال الواجب إلى دفعتك. تأكد من حله في الوقت المحدد.",
                NotificationCategory.Homework,
                "/Students/Homeworks"
            );

            var instructorId = await _context.InstructorCurriculumBatches
                .Where(x => x.BatchId == set.BatchId)
                .Select(x => x.InstructorId)
                .FirstOrDefaultAsync();

            if (instructorId > 0)
            {
                await _notificationService.SendToInstructorAsync(
                    instructorId,
                    $"📘 تم إعادة إرسال الواجب ({set.Title}) للدفعة {set.Batch?.Name}.",
                    NotificationCategory.Homework,
                    "/Instructors/HomeworkDashboard"
                );
            }

            TempData["Success"] = generated
                ? "✅ تم إعادة إرسال الواجب وتوليد الأسئلة للطلاب الجدد بنجاح."
                : "✅ تم إعادة إرسال الواجب بنجاح دون توليد جديد.";

            return RedirectToAction(nameof(Index));
        }





        [HttpPost]
        [AdminPermission("HomeworkManagement", "Delete")]

        public async Task<IActionResult> DeleteHomeworkSet(int id)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(id))
                return ArchivedAccessDenied();

            // 🟩 تأكيد وجود الواجب
            var homeworkSet = await _context.HomeworkSets
                .Include(hs => hs.Homeworks)
                .FirstOrDefaultAsync(hs => hs.Id == id);

            if (homeworkSet == null)
                return NotFound("لم يتم العثور على الواجب.");

            // 🟥 حذف الواجبات التابعة للطلاب
            var relatedHomeworks = await _context.Homeworks
                .Where(h => h.HomeworkSetId == id)
                .ToListAsync();

            if (relatedHomeworks.Any())
                _context.Homeworks.RemoveRange(relatedHomeworks);

            // 🔸 (اختياري) حذف المحاولات المرتبطة بالواجب
            var relatedAttempts = await _context.QuestionAttemptNew
                .Where(a => a.HomeworkSetId == id)
                .ToListAsync();

            if (relatedAttempts.Any())
                _context.QuestionAttemptNew.RemoveRange(relatedAttempts);

            // 🔸 حذف سجلات جلسات حل الواجب (FK Restrict يمنع الحذف بدونها)
            var relatedSessionLogs = await _context.HomeworkSessionLogs
                .Where(l => l.HomeworkSetId == id)
                .ToListAsync();

            if (relatedSessionLogs.Any())
                _context.HomeworkSessionLogs.RemoveRange(relatedSessionLogs);

            // 🟩 حذف الواجب نفسه
            _context.HomeworkSets.Remove(homeworkSet);

            await LogHomeworkArchiveActivityAsync(
                id,
                "ArchiveDelete",
                $"تم حذف الواجب المؤرشف '{homeworkSet.Title}' وكل بياناته.",
                homeworkSet.BatchId);

            await _context.SaveChangesAsync();

            // 📢 إشعار للطلاب بحذف الواجب
            var studentIds = relatedHomeworks
                .Select(h => h.StudentId)
                .Distinct()
                .ToList();

            if (studentIds.Any())
            {
                await _notificationService.SendToStudentsAsync(
                    studentIds,
                    "⚠️ تم حذف أحد الواجبات من قبل الإدارة ولن تحتاج إلى حله.",
                    NotificationCategory.Homework,
                    "/Students/Homeworks"
                );
            }

            TempData["Success"] = "✅ تم حذف الواجب وجميع بياناته من النظام.";
            return RedirectToAction(nameof(Index));
        }


        [HttpGet]
        [AdminPermission("HomeworkManagement", "Reports")]
        public async Task<IActionResult> HomeworkParentReport(int homeworkSetId, int studentId, bool print = false)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return ArchivedAccessDenied();

            using var db = _contextFactory.CreateDbContext();

            // =====================================================
            // 1) بيانات الطالب والدفعة
            // =====================================================
            var studentInfo = await db.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => new
                {
                    s.FullName,
                    BatchName = s.BatchEnrollments
                        .Select(e => e.Batch.Name)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            if (studentInfo == null)
                return NotFound();

            // =====================================================
            // 2) نتيجة الواجب من جدول الربط
            // =====================================================
            var homeworkStudentSummary = await db.HomeworkSetStudents
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.HomeworkSetId == homeworkSetId)
                .Select(x => new
                {
                    x.Score,
                    x.IsSubmitted,
                    x.SubmittedAt,
                    x.AssignedAt
                })
                .FirstOrDefaultAsync();

            // =====================================================
            // 3) الأسئلة المرتبطة بالواجب - المرجع الأساسي
            //    Join صريح لتجنب Lazy Loading و N+1
            // =====================================================
            var homeworkQuestions = await (
                from h in db.Homeworks.AsNoTracking()
                join q in db.Questions.AsNoTracking() on h.QuestionId equals q.Id
                join l in db.Lessons.AsNoTracking() on q.LessonId equals l.Id
                join s in db.Sections.AsNoTracking() on l.SectionId equals s.Id
                where h.StudentId == studentId
                      && h.HomeworkSetId == homeworkSetId
                      && l.IsActive
                select new
                {
                    HomeworkId = h.Id,
                    h.QuestionId,
                    QuestionTitle = q.Title,
                    q.CorrectAnswer,
                    LessonId = l.Id,
                    LessonTitle = l.Title,
                    SectionId = s.Id,
                    SectionTitle = s.Title,
                    HomeworkStudentAnswer = h.StudentAnswer,
                    HomeworkIsCorrect = h.IsCorrect,
                    HomeworkTimeSpentSeconds = h.TimeSpentSeconds,
                    h.StartTime,
                    h.AnsweredAt
                })
                .ToListAsync();

            // =====================================================
            // 4) كل المحاولات لهذا الطالب داخل هذا الواجب فقط
            //    لا توجد Query داخل Loop
            // =====================================================
            var attempts = await db.QuestionAttemptNew
                .AsNoTracking()
                .Where(a =>
                    a.StudentId == studentId &&
                    a.HomeworkSetId == homeworkSetId)
                .Select(a => new
                {
                    a.Id,
                    a.QuestionId,
                    a.SelectedAnswer,
                    a.IsCorrect,
                    a.TimeTakenSeconds,
                    a.AttemptedAt,
                    a.IsMarkedForReview,
                    a.AttemptNumber,
                    a.HomeworkSetAttemptId
                })
                .ToListAsync();

            var latestAttemptsByQuestionId = attempts
                .GroupBy(a => a.QuestionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(a => a.AttemptedAt).First());

            // =====================================================
            // 5) بناء قائمة الأسئلة التحليلية من الذاكرة
            // =====================================================
            var questions = homeworkQuestions
                .Select(q =>
                {
                    latestAttemptsByQuestionId.TryGetValue(q.QuestionId, out var lastAttempt);

                    var studentAnswer = lastAttempt != null
                        ? lastAttempt.SelectedAnswer
                        : q.HomeworkStudentAnswer;

                    var isCorrect = lastAttempt != null
                        ? lastAttempt.IsCorrect
                        : q.HomeworkIsCorrect == true;

                    var timeTakenSeconds = lastAttempt != null
                        ? lastAttempt.TimeTakenSeconds
                        : (q.HomeworkTimeSpentSeconds.HasValue ? q.HomeworkTimeSpentSeconds.Value : 0);

                    return new HomeworkQuestionAnalyticsVm
                    {
                        QuestionId = q.QuestionId,
                        QuestionTitle = q.QuestionTitle,
                        StudentAnswer = studentAnswer,
                        CorrectAnswer = q.CorrectAnswer,
                        IsCorrect = isCorrect,
                        IsRTL = true,
                        TimeTakenSeconds = timeTakenSeconds
                    };
                })
                .ToList();

            // =====================================================
            // 6) الحسابات العامة
            // =====================================================
            int totalQuestions = questions.Count;
            int correctCount = questions.Count(x => x.IsCorrect);
            int wrongCount = questions.Count(x => !x.IsCorrect && !string.IsNullOrWhiteSpace(x.StudentAnswer));
            int skippedCount = totalQuestions - correctCount - wrongCount;
            if (skippedCount < 0)
                skippedCount = 0;

            double totalSeconds = questions.Sum(x => x.TimeTakenSeconds);
            double timeSpentMinutes = totalSeconds > 0
                ? Math.Round(totalSeconds / 60.0, 1)
                : 0;

            double avgTimePerQuestion = questions.Any(x => x.TimeTakenSeconds > 0)
                ? Math.Round(questions.Where(x => x.TimeTakenSeconds > 0).Average(x => x.TimeTakenSeconds), 1)
                : 0;

            double scorePercentage = totalQuestions > 0
                ? Math.Round(correctCount * 100.0 / totalQuestions, 1)
                : 0;

            if (homeworkStudentSummary?.Score != null && homeworkStudentSummary.Score.Value > 0)
                scorePercentage = Math.Round(homeworkStudentSummary.Score.Value, 1);

            // =====================================================
            // 7) أداء المحاور
            // =====================================================
            var questionResultById = questions
                .GroupBy(x => x.QuestionId)
                .ToDictionary(g => g.Key, g => g.First());

            var sectionsPerformance = homeworkQuestions
                .GroupBy(x => new { x.SectionId, x.SectionTitle })
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(q =>
                    {
                        if (!questionResultById.TryGetValue(q.QuestionId, out var result))
                            return false;

                        return result.IsCorrect;
                    });

                    var wrong = g.Count(q =>
                    {
                        if (!questionResultById.TryGetValue(q.QuestionId, out var result))
                            return false;

                        return !result.IsCorrect && !string.IsNullOrWhiteSpace(result.StudentAnswer);
                    });

                    var skipped = total - correct - wrong;
                    if (skipped < 0)
                        skipped = 0;

                    var accuracy = total > 0
                        ? Math.Round(correct * 100.0 / total, 1)
                        : 0;

                    return new SectionPerformanceVm
                    {
                        SectionId = g.Key.SectionId,
                        SectionTitle = g.Key.SectionTitle,
                        Total = total,
                        Correct = correct,
                        Wrong = wrong,
                        Skipped = skipped,
                        Accuracy = accuracy,
                        ScorePercentage = accuracy
                    };
                })
                .OrderBy(x => x.SectionTitle)
                .ToList();

            var bestSection = sectionsPerformance
                .OrderByDescending(x => x.Accuracy)
                .Select(x => x.SectionTitle)
                .FirstOrDefault();

            // =====================================================
            // 8) أداء المؤشرات
            // =====================================================
            var lessonsPerformance = homeworkQuestions
                .GroupBy(x => new { x.LessonId, x.LessonTitle })
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(q =>
                    {
                        if (!questionResultById.TryGetValue(q.QuestionId, out var result))
                            return false;

                        return result.IsCorrect;
                    });

                    var seconds = g.Sum(q =>
                    {
                        if (!questionResultById.TryGetValue(q.QuestionId, out var result))
                            return 0;

                        return result.TimeTakenSeconds;
                    });

                    return new QdratNew.ViewModels.Homework.LessonPerformanceVm
                    {
                        LessonId = g.Key.LessonId,
                        LessonTitle = g.Key.LessonTitle,
                        TotalQuestions = total,
                        SuccessRate = total > 0
                            ? Math.Round(correct * 100.0 / total, 1)
                            : 0,
                        TimeSpentMinutes = seconds > 0
                            ? Math.Round(seconds / 60.0, 1)
                            : 0
                    };
                })
                .OrderBy(x => x.LessonTitle)
                .ToList();

            // =====================================================
            // 9) كروت المحاور والمؤشرات - بنفس بيانات التقرير المتقدم
            // =====================================================
            var sectionIndicatorCards = homeworkQuestions
                .GroupBy(x => new { x.SectionId, x.SectionTitle })
                .Select(sectionGroup =>
                {
                    var sectionTotal = sectionGroup.Count();
                    var sectionCorrect = sectionGroup.Count(q =>
                    {
                        if (!questionResultById.TryGetValue(q.QuestionId, out var result))
                            return false;

                        return result.IsCorrect;
                    });

                    var sectionWrong = sectionGroup.Count(q =>
                    {
                        if (!questionResultById.TryGetValue(q.QuestionId, out var result))
                            return false;

                        return !result.IsCorrect && !string.IsNullOrWhiteSpace(result.StudentAnswer);
                    });

                    var sectionSkipped = sectionTotal - sectionCorrect - sectionWrong;
                    if (sectionSkipped < 0)
                        sectionSkipped = 0;

                    return new HomeworkSectionIndicatorCardVm
                    {
                        SectionId = sectionGroup.Key.SectionId,
                        SectionTitle = sectionGroup.Key.SectionTitle,
                        TotalQuestions = sectionTotal,
                        CorrectCount = sectionCorrect,
                        WrongCount = sectionWrong,
                        SkippedCount = sectionSkipped,
                        Accuracy = sectionTotal == 0 ? 0 : Math.Round(sectionCorrect * 100.0 / sectionTotal, 1),
                        Indicators = sectionGroup
                            .GroupBy(x => new { x.LessonId, x.LessonTitle })
                            .Select(lessonGroup =>
                            {
                                var lessonTotal = lessonGroup.Count();
                                var lessonCorrect = lessonGroup.Count(q =>
                                {
                                    if (!questionResultById.TryGetValue(q.QuestionId, out var result))
                                        return false;

                                    return result.IsCorrect;
                                });

                                var lessonWrong = lessonGroup.Count(q =>
                                {
                                    if (!questionResultById.TryGetValue(q.QuestionId, out var result))
                                        return false;

                                    return !result.IsCorrect && !string.IsNullOrWhiteSpace(result.StudentAnswer);
                                });

                                var lessonSkipped = lessonTotal - lessonCorrect - lessonWrong;
                                if (lessonSkipped < 0)
                                    lessonSkipped = 0;

                                var lessonSeconds = lessonGroup.Sum(q =>
                                {
                                    if (!questionResultById.TryGetValue(q.QuestionId, out var result))
                                        return 0;

                                    return result.TimeTakenSeconds;
                                });

                                var lessonAccuracy = lessonTotal == 0
                                    ? 0
                                    : Math.Round(lessonCorrect * 100.0 / lessonTotal, 1);

                                return new HomeworkIndicatorCardVm
                                {
                                    LessonId = lessonGroup.Key.LessonId,
                                    LessonTitle = lessonGroup.Key.LessonTitle,
                                    QuestionCount = lessonTotal,
                                    CorrectCount = lessonCorrect,
                                    WrongCount = lessonWrong,
                                    SkippedCount = lessonSkipped,
                                    Accuracy = lessonAccuracy,
                                    AvgTimeSeconds = lessonTotal > 0
                                        ? Math.Round(lessonSeconds / lessonTotal, 1)
                                        : 0,
                                    HardnessIndex = Math.Round(100 - lessonAccuracy, 1)
                                };
                            })
                            .OrderBy(x => x.LessonTitle)
                            .ToList()
                    };
                })
                .OrderBy(x => x.SectionTitle)
                .ToList();

            // =====================================================
            // 10) تطور الطالب
            // =====================================================
            var previousScores = await db.HomeworkSetStudents
                .AsNoTracking()
                .Where(x => x.StudentId == studentId && x.Score.HasValue)
                .OrderBy(x => x.SubmittedAt)
                .Select(x => x.Score.Value)
                .ToListAsync();

            string progress = "لا يوجد بيانات كافية";

            if (previousScores.Count >= 2)
            {
                var last = previousScores[previousScores.Count - 1];
                var previous = previousScores[previousScores.Count - 2];

                if (last > previous)
                    progress = "📈 تحسن";
                else if (last < previous)
                    progress = "📉 تراجع";
                else
                    progress = "➖ ثابت";
            }

            // =====================================================
            // 11) كشف السلوك والتلاعب
            // =====================================================
            var behavior = _analyticsService.AnalyzeBehavior(
                avgTimePerQuestion,
                correctCount,
                totalQuestions
            );

            var parentReport = _analyticsService.BuildParentReport(
                scorePercentage,
                correctCount,
                wrongCount,
                skippedCount,
                avgTimePerQuestion,
                behavior.level,
                progress
            );

            var recommendation = _homeworkRecommendationService.GenerateRecommendation(
                new HomeworkAnalyticsVm
                {
                    ScorePercentage = scorePercentage,
                    TimeSpentMinutes = timeSpentMinutes,
                    QuestionsCount = totalQuestions,
                    CorrectAnswers = correctCount
                });

            // =====================================================
            // 12) ViewModel
            // =====================================================
            var vm = new HomeworkAnalyticsViewModel
            {
                HomeworkSetId = homeworkSetId,
                StudentId = studentId,
                StudentName = studentInfo.FullName,
                BatchName = studentInfo.BatchName ?? "غير محددة",
                Questions = questions,

                TotalQuestions = totalQuestions,
                CorrectAnswers = correctCount,
                WrongAnswers = wrongCount,
                SkippedAnswers = skippedCount,

                ScorePercentage = scorePercentage,
                TimeSpentMinutes = timeSpentMinutes,
                AvgTimePerQuestion = avgTimePerQuestion,

                BestSection = bestSection,
                SectionsPerformance = sectionsPerformance,
                LessonsPerformance = lessonsPerformance,
                SectionIndicatorCards = sectionIndicatorCards,

                BehaviorLevel = behavior.level,
                BehaviorAnalysisText = behavior.text,
                ParentReport = parentReport,
                Recommendation = recommendation,
                ProgressTimeline = await _analyticsService.GetStudentProgressAsync(studentId)
            };

            if (print)
                return View("HomeworkParentReport_Print", vm);

            return View("HomeworkParentReport", vm);
        }



        [HttpGet]
        public async Task<IActionResult> SectionLessons(int homeworkSetId, int sectionId, int studentId)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return ArchivedAccessDenied();

            var section = await _context.Sections
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sectionId);

            if (section == null)
                return NotFound("❌ لم يتم العثور على المحور.");

            var homeworkExists = await _context.HomeworkSets
                .AsNoTracking()
                .AnyAsync(x => x.Id == homeworkSetId);

            if (!homeworkExists)
                return NotFound("❌ لم يتم العثور على الواجب.");

            var attemptItems = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Include(a => a.Question)
                    .ThenInclude(q => q.Lesson)
                .Where(a =>
                    a.HomeworkSetId == homeworkSetId &&
                    a.StudentId == studentId &&
                    a.Question.Lesson.SectionId == sectionId)
                .ToListAsync();

            var latestAttemptItems = attemptItems
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(a => a.AttemptedAt).First())
                .ToList();

            var lessons = latestAttemptItems
                .GroupBy(a => new
                {
                    LessonId = a.Question.Lesson.Id,
                    LessonTitle = a.Question.Lesson.Title
                })
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(a => a.IsCorrect);
                    var accuracy = total == 0 ? 0 : Math.Round(correct * 100.0 / total, 1);
                    var timeValues = g.Select(a => a.TimeTakenSeconds).ToList();

                    return new QdratNew.ViewModels.Exam.LessonPerformanceVm
                    {
                        LessonId = g.Key.LessonId,
                        LessonTitle = g.Key.LessonTitle,
                        QuestionCount = total,
                        AvgSuccessRate = accuracy,
                        AvgTimeSeconds = timeValues.Any() ? Math.Round(timeValues.Average(), 1) : 0,
                        HardnessIndex = Math.Round(100 - accuracy, 1)
                    };
                })
                .OrderBy(x => x.LessonTitle)
                .ToList();

            if (!lessons.Any())
            {
                var items = await _context.Homeworks
                    .AsNoTracking()
                    .Include(h => h.Question)
                        .ThenInclude(q => q.Lesson)
                    .Where(h =>
                        h.HomeworkSetId == homeworkSetId &&
                        h.StudentId == studentId &&
                        h.Question.Lesson.SectionId == sectionId)
                    .ToListAsync();

                lessons = items
                    .GroupBy(h => new
                    {
                        LessonId = h.Question.Lesson.Id,
                        LessonTitle = h.Question.Lesson.Title
                    })
                    .Select(g =>
                    {
                        var total = g.Count();
                        var correct = g.Count(h => h.IsCorrect == true);
                        var accuracy = total == 0 ? 0 : Math.Round(correct * 100.0 / total, 1);
                        var timeValues = g.Where(h => h.TimeSpentSeconds.HasValue).Select(h => h.TimeSpentSeconds!.Value).ToList();

                        return new QdratNew.ViewModels.Exam.LessonPerformanceVm
                        {
                            LessonId = g.Key.LessonId,
                            LessonTitle = g.Key.LessonTitle,
                            QuestionCount = total,
                            AvgSuccessRate = accuracy,
                            AvgTimeSeconds = timeValues.Any() ? Math.Round(timeValues.Average(), 1) : 0,
                            HardnessIndex = Math.Round(100 - accuracy, 1)
                        };
                    })
                    .OrderBy(x => x.LessonTitle)
                    .ToList();
            }

            var model = new QdratNew.ViewModels.Exam.SectionLessonsViewModel
            {
                StudentId = studentId,
                HomeworkSetId = homeworkSetId,
                AssignmentId = homeworkSetId,
                SectionId = section.Id,
                SectionTitle = section.Title,
                Lessons = lessons
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> HomeworkLessonQuestionsDetail(int homeworkSetId, int lessonId, int studentId)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return ArchivedAccessDenied();

            var lesson = await _context.Lessons
                .AsNoTracking()
                .Include(l => l.Section)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null)
                return NotFound("❌ لم يتم العثور على المؤشر.");

            var attemptItems = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Include(a => a.Question)
                    .ThenInclude(q => q.Options)
                .Include(a => a.Question.VerbalPassage)
                .Where(a =>
                    a.HomeworkSetId == homeworkSetId &&
                    a.StudentId == studentId &&
                    a.Question.LessonId == lessonId)
                .ToListAsync();

            var latestAttemptItems = attemptItems
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(a => a.AttemptedAt).First())
                .ToList();

            if (latestAttemptItems.Any())
            {
                var attemptQuestionVms = latestAttemptItems.Select(a =>
                {
                    var question = a.Question;
                    var displayType = question.Template switch
                    {
                        QuestionTemplate.CompareValues => QuestionDisplayType.ComparisonText,
                        QuestionTemplate.CompareWithImage => QuestionDisplayType.ComparisonWithImage,
                        _ => QuestionDisplayType.WithImage
                    };

                    return new AdminSectionQuestionVm
                    {
                        SectionTitle = lesson.Section.Title,
                        LessonTitle = lesson.Title,
                        QuestionTitle = question.Title,
                        ImageUrl = question.ImageUrl,
                        VerbalPassageContent = question.VerbalPassage?.Content,
                        ComparisonValue1 = question.ValueA,
                        ComparisonValue2 = question.ValueB,
                        DisplayType = displayType,
                        IsQuantitative = question.IsQuantitative,
                        CorrectAnswer = question.CorrectAnswer,
                        StudentAnswer = a.SelectedAnswer,
                        IsCorrect = a.IsCorrect,
                        Options = question.Options.Select(o => new QuestionOptionVm
                        {
                            Text = o.Text,
                            ImageUrl = o.ImageUrl,
                            IsCorrect = o.Text == question.CorrectAnswer,
                            IsSelectedByStudent = o.Text == a.SelectedAnswer
                        }).ToList()
                    };
                }).ToList();

                return View(attemptQuestionVms);
            }

            var items = await _context.Homeworks
                .AsNoTracking()
                .Include(h => h.Question)
                    .ThenInclude(q => q.Options)
                .Include(h => h.Question.VerbalPassage)
                .Where(h =>
                    h.HomeworkSetId == homeworkSetId &&
                    h.StudentId == studentId &&
                    h.Question.LessonId == lessonId)
                .ToListAsync();

            if (!items.Any())
                return Content("<div class='alert alert-warning text-center'>⚠️ لا توجد أسئلة لهذا المؤشر داخل هذا الواجب.</div>", "text/html");

            var questionVms = items.Select(h =>
            {
                var question = h.Question;
                var displayType = question.Template switch
                {
                    QuestionTemplate.CompareValues => QuestionDisplayType.ComparisonText,
                    QuestionTemplate.CompareWithImage => QuestionDisplayType.ComparisonWithImage,
                    _ => QuestionDisplayType.WithImage
                };

                return new AdminSectionQuestionVm
                {
                    SectionTitle = lesson.Section.Title,
                    LessonTitle = lesson.Title,
                    QuestionTitle = question.Title,
                    ImageUrl = question.ImageUrl,
                    VerbalPassageContent = question.VerbalPassage?.Content,
                    ComparisonValue1 = question.ValueA,
                    ComparisonValue2 = question.ValueB,
                    DisplayType = displayType,
                    IsQuantitative = question.IsQuantitative,
                    CorrectAnswer = question.CorrectAnswer,
                    StudentAnswer = h.StudentAnswer,
                    IsCorrect = h.IsCorrect == true,
                    Options = question.Options.Select(o => new QuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl,
                        IsCorrect = o.Text == question.CorrectAnswer,
                        IsSelectedByStudent = o.Text == h.StudentAnswer
                    }).ToList()
                };
            }).ToList();

            return View(questionVms);
        }



        [HttpGet]
        public async Task<IActionResult> ReviewLesson(int homeworkSetId, int lessonId, int? studentId = null)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return ArchivedAccessDenied();

            if (!studentId.HasValue || studentId.Value <= 0)
                return BadRequest("لا يمكن مراجعة أسئلة المؤشر بدون تحديد الطالب.");

            var homeworkSet = await _context.HomeworkSets
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == homeworkSetId);

            if (homeworkSet == null)
                return NotFound("❌ لم يتم العثور على الواجب.");

            var lesson = await _context.Lessons
                .AsNoTracking()
                .Include(l => l.Section)
                .FirstOrDefaultAsync(l => l.Id == lessonId && l.IsActive);

            if (lesson == null)
                return NotFound("❌ لم يتم العثور على المؤشر.");

            var homeworkQuestionsQuery = _context.Homeworks
                .AsNoTracking()
                .Include(h => h.Question)
                    .ThenInclude(q => q.Options)
                .Include(h => h.Question)
                    .ThenInclude(q => q.VerbalPassage)
                .Where(h =>
                    h.HomeworkSetId == homeworkSetId &&
                    h.StudentId == studentId.Value &&
                    h.Question.LessonId == lessonId &&
                    h.Question.Lesson.IsActive);

            var homeworkQuestionItems = await homeworkQuestionsQuery
                .ToListAsync();

            var questions = homeworkQuestionItems
                .Where(h => h.Question != null)
                .Select(h => h.Question)
                .GroupBy(q => q.Id)
                .Select(g => g.First())
                .ToList();

            var totalStudents = homeworkQuestionItems.Any() ? 1 : 0;

            // جلب محاولات الطلاب - QuestionAttemptNew أولاً
            var attemptItemsQuery = _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a =>
                    a.HomeworkSetId == homeworkSetId &&
                    a.StudentId == studentId.Value &&
                    a.Question.LessonId == lessonId &&
                    a.Question.Lesson.IsActive);

            var attemptItems = await attemptItemsQuery.ToListAsync();

            // آخر محاولة لكل طالب لكل سؤال
            var latestAttempts = attemptItems
                .GroupBy(a => new { a.StudentId, a.QuestionId })
                .Select(g => g.OrderByDescending(a => a.AttemptedAt).First())
                .ToList();

            // إذا لم توجد محاولات، نجلب من جدول Homeworks
            Dictionary<Guid, List<(string? Answer, bool IsCorrect)>> answersByQuestion;

            if (latestAttempts.Any())
            {
                answersByQuestion = latestAttempts
                    .GroupBy(a => a.QuestionId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(a => (a.SelectedAnswer, a.IsCorrect)).ToList()
                    );
            }
            else
            {
                var homeworkAnswersQuery = _context.Homeworks
                    .AsNoTracking()
                    .Where(h =>
                        h.HomeworkSetId == homeworkSetId &&
                        h.StudentId == studentId.Value &&
                        h.Question.LessonId == lessonId &&
                        h.Question.Lesson.IsActive &&
                        h.IsCompleted);

                var homeworkAnswers = await homeworkAnswersQuery.ToListAsync();

                answersByQuestion = homeworkAnswers
                    .GroupBy(h => h.QuestionId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(h => (h.StudentAnswer, h.IsCorrect == true)).ToList()
                    );
            }

            var questionVms = questions.Select(q =>
            {
                var displayType = q.Template switch
                {
                    QuestionTemplate.CompareValues => QuestionDisplayType.ComparisonText,
                    QuestionTemplate.CompareWithImage => QuestionDisplayType.ComparisonWithImage,
                    _ => QuestionDisplayType.WithImage
                };

                var answers = answersByQuestion.TryGetValue(q.Id, out var ans) ? ans : new();
                var totalAnswered = answers.Count;
                var correctCount = answers.Count(a => a.IsCorrect);
                var wrongCount = totalAnswered - correctCount;

                var optionVms = q.Options.Select(o =>
                {
                    var selectedCount = answers.Count(a => a.Answer == o.Text);
                    return new QdratNew.ViewModels.Homework.ReviewLessonOptionVm
                    {
                        Text = o.Text ?? "",
                        ImageUrl = o.ImageUrl,
                        IsCorrect = o.Text == q.CorrectAnswer,
                        SelectedCount = selectedCount,
                        SelectionRate = totalAnswered == 0 ? 0 : Math.Round(selectedCount * 100.0 / totalAnswered, 1)
                    };
                }).ToList();

                return new QdratNew.ViewModels.Homework.ReviewLessonQuestionVm
                {
                    QuestionId = q.Id,
                    QuestionTitle = q.Title,
                    ImageUrl = q.ImageUrl,
                    VerbalPassageTitle = q.VerbalPassage?.Title,
                    VerbalPassageContent = q.VerbalPassage?.Content,
                    ComparisonValue1 = q.ValueA,
                    ComparisonValue2 = q.ValueB,
                    DisplayType = displayType,
                    IsQuantitative = q.IsQuantitative,
                    CorrectAnswer = q.CorrectAnswer,
                    TotalAnswered = totalAnswered,
                    CorrectCount = correctCount,
                    WrongCount = wrongCount,
                    Options = optionVms
                };
            }).ToList();

            var model = new QdratNew.ViewModels.Homework.ReviewLessonViewModel
            {
                HomeworkSetId = homeworkSetId,
                HomeworkSetTitle = homeworkSet.Title,
                LessonId = lessonId,
                LessonTitle = lesson.Title,
                SectionTitle = lesson.Section?.Title ?? "",
                TotalStudents = totalStudents,
                Questions = questionVms
            };

            return View(model);
        }

        [HttpPost]
        [AdminPermission("HomeworkManagement", "Resend")]
        public async Task<IActionResult> ResendToStudent(int homeworkId)
        {
            var homeworkSetIdValue = await _homeworkService.GetHomeworkSetIdFromHomework(homeworkId);
            if (!homeworkSetIdValue.HasValue)
                return NotFound("لم يتم العثور على الواجب.");

            var homeworkSetId = homeworkSetIdValue.Value;
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return ArchivedAccessDenied();

            var success = await _homeworkService.ResendHomeworkToStudentAsync(homeworkId, User.GetUserIdAsInt());
            if (!success)
                return BadRequest("تعذر إعادة إرسال الواجب.");

            // 📢 إشعار لطالب واحد
            var studentId = await _context.Homeworks
                .Where(h => h.Id == homeworkId)
                .Select(h => h.StudentId)
                .FirstOrDefaultAsync();

            await _notificationService.SendToStudentAsync(studentId,
                "📢 تم إعادة إرسال الواجب الخاص بك. تأكد من حله.",
                NotificationCategory.Homework,
                "/Students/Homeworks");

            await LogHomeworkArchiveActivityAsync(
                homeworkSetId,
                "ArchiveEdit",
                $"تمت إعادة إرسال واجب مؤرشف للطالب رقم {studentId}.");

            return RedirectToAction(nameof(Details), new { id = homeworkSetId });
        }

        [HttpGet]
        public async Task<IActionResult> ManageArchiveAccess(int homeworkSetId)
        {
            if (!IsArchiveOwner())
                return Forbid();

            var homeworkSet = await _context.HomeworkSets
                .AsNoTracking()
                .Include(x => x.Batch)
                .FirstOrDefaultAsync(x => x.Id == homeworkSetId);

            if (homeworkSet == null)
                return NotFound();

            if (!homeworkSet.IsArchived)
            {
                TempData["Error"] = "إدارة موافقات الأرشيف متاحة للواجبات المؤرشفة فقط.";
                return RedirectToAction(nameof(Index));
            }

            var users = await GetArchiveApprovalCandidateUsersAsync();
            var activeAccessUserIds = (await _context.HomeworkArchiveAccesses
                    .AsNoTracking()
                    .Where(x => x.HomeworkSetId == homeworkSetId && x.IsActive)
                    .Select(x => x.UserId)
                    .ToListAsync())
                .ToHashSet();

            var items = new List<HomeworkArchiveUserAccessItem>();
            foreach (var user in users.OrderBy(x => x.FullName ?? x.UserName))
            {
                var roles = await _userManager.GetRolesAsync(user);
                items.Add(new HomeworkArchiveUserAccessItem
                {
                    UserId = user.Id,
                    DisplayName = user.FullName ?? user.UserName ?? user.Email ?? user.Id,
                    Email = user.Email ?? string.Empty,
                    Roles = string.Join("، ", roles),
                    IsAllowed = activeAccessUserIds.Contains(user.Id)
                });
            }

            var model = new HomeworkArchiveAccessViewModel
            {
                HomeworkSetId = homeworkSet.Id,
                HomeworkTitle = homeworkSet.Title,
                BatchName = homeworkSet.Batch?.Name ?? string.Empty,
                Users = items
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateArchiveAccess(int homeworkSetId, List<string> allowedUserIds)
        {
            if (!IsArchiveOwner())
                return Forbid();

            var homeworkSet = await _context.HomeworkSets
                .FirstOrDefaultAsync(x => x.Id == homeworkSetId);

            if (homeworkSet == null)
                return NotFound();

            if (!homeworkSet.IsArchived)
            {
                TempData["Error"] = "لا يمكن تعديل موافقات الأرشيف لواجب غير مؤرشف.";
                return RedirectToAction(nameof(Index));
            }

            allowedUserIds ??= new List<string>();
            var allowedSet = allowedUserIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToHashSet();

            var candidateUsers = await GetArchiveApprovalCandidateUsersAsync();
            var candidateUserIds = candidateUsers.Select(x => x.Id).ToHashSet();
            allowedSet.RemoveWhere(x => !candidateUserIds.Contains(x));

            var existing = await _context.HomeworkArchiveAccesses
                .Where(x => x.HomeworkSetId == homeworkSetId)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var currentUserId = CurrentUserId();

            foreach (var access in existing)
            {
                access.IsActive = allowedSet.Contains(access.UserId);
            }

            var existingUserIds = existing.Select(x => x.UserId).ToHashSet();
            foreach (var userId in allowedSet.Where(x => !existingUserIds.Contains(x)))
            {
                _context.HomeworkArchiveAccesses.Add(new HomeworkArchiveAccess
                {
                    HomeworkSetId = homeworkSetId,
                    UserId = userId,
                    GrantedByUserId = currentUserId,
                    GrantedAt = now,
                    IsActive = true
                });
            }

            await _context.SaveChangesAsync();

            await LogHomeworkArchiveActivityAsync(
                homeworkSetId,
                "ArchiveAccessUpdated",
                $"تم تعديل موافقات الوصول للواجب المؤرشف '{homeworkSet.Title}'. عدد المستخدمين المصرح لهم: {allowedSet.Count}.",
                homeworkSet.BatchId);

            TempData["Success"] = "تم تحديث موافقات الوصول للأرشيف.";
            return RedirectToAction(nameof(ManageArchiveAccess), new { homeworkSetId });
        }

        [HttpGet]
        public async Task<IActionResult> ArchiveHistory(int homeworkSetId)
        {
            if (!await CanAccessArchivedHomeworkSetAsync(homeworkSetId))
                return ArchivedAccessDenied();

            var homeworkSet = await _context.HomeworkSets
                .AsNoTracking()
                .Include(x => x.Batch)
                .FirstOrDefaultAsync(x => x.Id == homeworkSetId);

            if (homeworkSet == null)
                return NotFound();

            var items = await _context.AdminActivityLogs
                .AsNoTracking()
                .Where(x => x.HomeworkSetId == homeworkSetId)
                .OrderByDescending(x => x.Timestamp)
                .Select(x => new HomeworkArchiveHistoryItem
                {
                    Timestamp = x.Timestamp,
                    AdminName = x.AdminName,
                    ActionType = x.ActionType,
                    Description = x.Description
                })
                .ToListAsync();

            var model = new HomeworkArchiveHistoryViewModel
            {
                HomeworkSetId = homeworkSet.Id,
                HomeworkTitle = homeworkSet.Title,
                BatchName = homeworkSet.Batch?.Name ?? string.Empty,
                Items = items
            };

            return View(model);
        }

        // =====================================================
        // إدارة صلاحيات الوصول لأرشيف الواجبات على مستوى الدفعة (Owner/Developer فقط)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ManageArchiveAccessByBatch(int batchId)
        {
            if (!IsArchiveOwner())
                return Forbid();

            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null) return NotFound();

            var archivedHomeworkSetIds = await _context.HomeworkSets
                .AsNoTracking()
                .Where(hs => hs.BatchId == batchId && hs.IsArchived)
                .Select(hs => hs.Id)
                .ToListAsync();

            if (!archivedHomeworkSetIds.Any())
            {
                TempData["Error"] = "إدارة الصلاحيات متاحة للدفعات التي تحتوي على واجبات مؤرشفة فقط.";
                return RedirectToAction(nameof(Archived));
            }

            var users = await GetArchiveApprovalCandidateUsersAsync();
            var allowedUserIds = (await _context.HomeworkArchiveAccesses
                .AsNoTracking()
                .Where(x => archivedHomeworkSetIds.Contains(x.HomeworkSetId) && x.IsActive)
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync()).ToHashSet();

            var items = new List<HomeworkArchiveUserAccessItem>();
            foreach (var user in users.OrderBy(x => x.FullName ?? x.UserName))
            {
                var roles = await _userManager.GetRolesAsync(user);
                items.Add(new HomeworkArchiveUserAccessItem
                {
                    UserId      = user.Id,
                    DisplayName = user.FullName ?? user.UserName ?? user.Email ?? "؟",
                    Email       = user.Email ?? string.Empty,
                    Roles       = string.Join("، ", roles),
                    IsAllowed   = allowedUserIds.Contains(user.Id)
                });
            }

            var model = new HomeworkBatchArchiveAccessViewModel
            {
                BatchId    = batch.Id,
                BatchName  = batch.Name,
                CourseTitle = batch.Course?.Name ?? string.Empty,
                Users      = items
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateArchiveAccessByBatch(int batchId, List<string> allowedUserIds)
        {
            if (!IsArchiveOwner())
                return Forbid();

            var batch = await _context.Batches.FirstOrDefaultAsync(b => b.Id == batchId);
            if (batch == null) return NotFound();

            allowedUserIds ??= new List<string>();
            var allowedSet = allowedUserIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct().ToHashSet();

            var candidateUsers   = await GetArchiveApprovalCandidateUsersAsync();
            var candidateUserIds = candidateUsers.Select(x => x.Id).ToHashSet();
            allowedSet.RemoveWhere(x => !candidateUserIds.Contains(x));

            var archivedHomeworkSetIds = await _context.HomeworkSets
                .AsNoTracking()
                .Where(hs => hs.BatchId == batchId && hs.IsArchived)
                .Select(hs => hs.Id)
                .ToListAsync();

            var now           = DateTime.UtcNow;
            var currentUserId = CurrentUserId();

            foreach (var homeworkSetId in archivedHomeworkSetIds)
            {
                var existing        = await _context.HomeworkArchiveAccesses
                    .Where(x => x.HomeworkSetId == homeworkSetId)
                    .ToListAsync();
                var existingUserIds = existing.Select(x => x.UserId).ToHashSet();

                foreach (var access in existing)
                    access.IsActive = allowedSet.Contains(access.UserId);

                foreach (var userId in allowedSet.Where(x => !existingUserIds.Contains(x)))
                {
                    _context.HomeworkArchiveAccesses.Add(new HomeworkArchiveAccess
                    {
                        HomeworkSetId   = homeworkSetId,
                        UserId          = userId,
                        GrantedByUserId = currentUserId,
                        GrantedAt       = now,
                        IsActive        = true
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ تم تحديث صلاحيات الوصول لأرشيف الدفعة. عدد المصرح لهم: {allowedSet.Count}.";
            return RedirectToAction(nameof(ManageArchiveAccessByBatch), new { batchId });
        }

        [HttpGet]
        [AdminPermission("HomeworkManagement", "Reports")]
        public async Task<IActionResult> BatchPerformanceReport(
            int batchId,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool print = false)
        {
            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null) return NotFound();

            // ── حساب الفترة ──────────────────────────────────────────────
            var yesterday = DateTime.Today.AddDays(-1);
            var rangeStart = fromDate?.Date ?? yesterday;
            var rangeEndInclusive = toDate?.Date ?? yesterday;
            if (rangeEndInclusive > yesterday) rangeEndInclusive = yesterday;
            var rangeEndExclusive = rangeEndInclusive.AddDays(1);

            bool isYesterdayOnly = (rangeStart == yesterday && rangeEndInclusive == yesterday);

            // رسالة تحذير إذا قُلِّص التاريخ
            if (toDate.HasValue && toDate.Value.Date > yesterday)
                TempData["Warning"] = "تاريخ النهاية المحدد يتجاوز الأمس — تم تقليصه تلقائيًا إلى نهاية يوم أمس.";

            // ── طلاب الدفعة ──────────────────────────────────────────────
            var students = await (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join s in _context.Students.AsNoTracking() on e.StudentID equals s.StudentID
                join u in _context.Users.AsNoTracking() on s.UserId equals u.Id
                where e.BatchId == batchId && u.IsActive
                orderby s.FullName
                select new { s.StudentID, s.FullName, s.PhoneNumber }
            ).ToListAsync();

            var studentIds = students.Select(s => s.StudentID).ToList();

            // ── محاضرات الدفعة في الفترة ──────────────────────────────────
            var lectures = await _context.Lecture
                .AsNoTracking()
                .Where(l => l.BatchId == batchId && l.Date >= rangeStart && l.Date < rangeEndExclusive)
                .OrderBy(l => l.Date)
                .Select(l => new { l.Id, l.Title, l.Date })
                .ToListAsync();

            var lectureIds = lectures.Select(l => l.Id).ToList();

            // ── سجلات الحضور ──────────────────────────────────────────────
            var attendanceRecs = await _context.AttendanceRecords
                .AsNoTracking()
                .Where(a => lectureIds.Contains(a.LectureId) && studentIds.Contains(a.StudentId))
                .Select(a => new { a.StudentId, a.LectureId, a.IsPresent, a.IsLateArrival })
                .ToListAsync();

            // ── واجبات الدفعة في الفترة ───────────────────────────────────
            var hwSets = await _context.HomeworkSets
                .AsNoTracking()
                .Where(hs => hs.BatchId == batchId && !hs.IsArchived
                             && hs.CreatedAt >= rangeStart && hs.CreatedAt < rangeEndExclusive)
                .OrderBy(hs => hs.CreatedAt)
                .Select(hs => new { hs.Id, hs.Title, hs.CompletionTitle, hs.CreatedAt, hs.EndAt, hs.IsClosed, hs.CurriculumId })
                .ToListAsync();

            var hwSetIds = hwSets.Select(hs => hs.Id).ToList();

            // ── سجلات طلاب الواجبات ───────────────────────────────────────
            var hwStudentRecs = await _context.HomeworkSetStudents
                .AsNoTracking()
                .Where(r => hwSetIds.Contains(r.HomeworkSetId) && studentIds.Contains(r.StudentId))
                .Select(r => new { r.HomeworkSetId, r.StudentId, r.IsSubmitted, r.Score })
                .ToListAsync();

            // ── مدرّبو المناهج (إعادة استخدام نفس منطق BatchHomeworks) ──────
            var courseCurriculumIds = await _context.CourseCurriculums
                .AsNoTracking()
                .Where(cc => cc.CourseId == batch.CourseId)
                .Select(cc => cc.CurriculumId)
                .ToListAsync();

            var curriculumIds = courseCurriculumIds;

            var curriculumTitleMap = curriculumIds.Any()
                ? await _context.Curriculums
                    .AsNoTracking()
                    .Where(c => curriculumIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.Title)
                : new Dictionary<int, string>();

            var instructorMap = new Dictionary<int, string>();
            if (curriculumIds.Any())
            {
                var instructorRawList = await (
                    from icb in _context.InstructorCurriculumBatches.AsNoTracking()
                    join ins in _context.Instructors.AsNoTracking() on icb.InstructorId equals ins.Id
                    where icb.BatchId == batchId && curriculumIds.Contains(icb.CurriculumId)
                    select new { icb.CurriculumId, InstructorName = ins.FullName }
                ).ToListAsync();

                instructorMap = instructorRawList
                    .GroupBy(x => x.CurriculumId)
                    .ToDictionary(g => g.Key, g => g.First().InstructorName);
            }

            var curriculumInstructors = curriculumIds
                .Where(cid => curriculumTitleMap.ContainsKey(cid))
                .Select(cid => new CurriculumInstructorVM
                {
                    CurriculumId = cid,
                    CurriculumTitle = curriculumTitleMap[cid],
                    InstructorName = instructorMap.GetValueOrDefault(cid, "")
                }).ToList();

            // ── بناء ViewModel للمحاضرات ──────────────────────────────────
            var lecturVMs = lectures.Select(l =>
            {
                var recs = attendanceRecs.Where(a => a.LectureId == l.Id).ToList();
                var presentCount = recs.Count(a => a.IsPresent && !a.IsLateArrival);
                var lateCount = recs.Count(a => a.IsPresent && a.IsLateArrival);
                var absentCount = students.Count - recs.Count(a => a.IsPresent);
                if (absentCount < 0) absentCount = 0;
                var attendanceRate = students.Count > 0
                    ? Math.Round((presentCount + lateCount) * 100.0 / students.Count, 1)
                    : 0;
                return new PerformanceLectureVM
                {
                    LectureId = l.Id,
                    Title = l.Title,
                    Date = l.Date,
                    PresentCount = presentCount,
                    LateCount = lateCount,
                    AbsentCount = absentCount,
                    TotalStudents = students.Count,
                    AttendanceRate = attendanceRate
                };
            }).ToList();

            // ── بناء ViewModel للواجبات ───────────────────────────────────
            var homeworkVMs = hwSets.Select(hs =>
            {
                var recs = hwStudentRecs.Where(r => r.HomeworkSetId == hs.Id).ToList();
                var totalAssigned = recs.Count;
                var submittedCount = recs.Count(r => r.IsSubmitted);
                var submissionRate = totalAssigned > 0 ? Math.Round(submittedCount * 100.0 / totalAssigned, 1) : 0;
                var scores = recs.Where(r => r.IsSubmitted && r.Score.HasValue).Select(r => r.Score!.Value).ToList();
                var avgScore = scores.Any() ? Math.Round(scores.Average(), 1) : 0;
                return new PerformanceHomeworkVM
                {
                    HomeworkSetId = hs.Id,
                    Title = !string.IsNullOrEmpty(hs.CompletionTitle) ? hs.CompletionTitle : hs.Title,
                    CreatedAt = hs.CreatedAt,
                    EndAt = hs.EndAt,
                    IsClosed = hs.IsClosed,
                    TotalAssigned = totalAssigned,
                    SubmittedCount = submittedCount,
                    NotSubmittedCount = totalAssigned - submittedCount,
                    SubmissionRate = submissionRate,
                    AverageScore = avgScore,
                    CurriculumId = hs.CurriculumId ?? 0
                };
            }).ToList();

            // ── تصنيف الطلاب ──────────────────────────────────────────────
            var hwTitleMap = hwSets.ToDictionary(
                hs => hs.Id,
                hs => !string.IsNullOrEmpty(hs.CompletionTitle) ? hs.CompletionTitle : hs.Title);

            var studentsAbove60 = new List<StudentPerformanceRowVM>();
            var studentsBelow60 = new List<StudentPerformanceRowVM>();
            var studentsNotSubmitted = new List<StudentPerformanceRowVM>();

            foreach (var s in students)
            {
                var sRecs = hwStudentRecs.Where(r => r.StudentId == s.StudentID).ToList();
                var assigned = sRecs.Count;
                var submitted = sRecs.Count(r => r.IsSubmitted);
                var scores = sRecs.Where(r => r.IsSubmitted && r.Score.HasValue).Select(r => r.Score!.Value).ToList();
                var avgScore = scores.Any() ? Math.Round(scores.Average(), 1) : 0;

                var sAtt = attendanceRecs.Where(a => a.StudentId == s.StudentID).ToList();
                var attendedCount = sAtt.Count(a => a.IsPresent);

                var missingTitles = sRecs
                    .Where(r => !r.IsSubmitted)
                    .Select(r => hwTitleMap.GetValueOrDefault(r.HomeworkSetId, ""))
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();

                var row = new StudentPerformanceRowVM
                {
                    StudentId = s.StudentID,
                    FullName = s.FullName,
                    PhoneNumber = s.PhoneNumber,
                    AverageScore = avgScore,
                    SubmittedCount = submitted,
                    AssignedCount = assigned,
                    AttendedCount = attendedCount,
                    TotalLecturesInPeriod = lectures.Count,
                    MissingHomeworkTitles = missingTitles
                };

                if (assigned > 0 && submitted == 0)
                    studentsNotSubmitted.Add(row);
                else if (submitted > 0 && avgScore >= 60)
                    studentsAbove60.Add(row);
                else if (submitted > 0 && avgScore < 60)
                    studentsBelow60.Add(row);
            }

            // ── الإحصائيات الكلية ─────────────────────────────────────────
            double overallAttendanceRate = 0;
            if (lectures.Any() && students.Any())
            {
                var totalPresent = attendanceRecs.Count(a => a.IsPresent);
                overallAttendanceRate = Math.Round(totalPresent * 100.0 / (lectures.Count * students.Count), 1);
            }

            double overallSubmissionRate = 0;
            double overallAverageScore = 0;
            if (hwStudentRecs.Any())
            {
                var total = hwStudentRecs.Count;
                var submitted = hwStudentRecs.Count(r => r.IsSubmitted);
                overallSubmissionRate = total > 0 ? Math.Round(submitted * 100.0 / total, 1) : 0;
                var allScores = hwStudentRecs.Where(r => r.IsSubmitted && r.Score.HasValue).Select(r => r.Score!.Value).ToList();
                overallAverageScore = allScores.Any() ? Math.Round(allScores.Average(), 1) : 0;
            }

            // ── بناء الـ ViewModel النهائي ────────────────────────────────
            var vm = new BatchPerformanceReportVM
            {
                BatchId = batchId,
                BatchName = batch.Name,
                CourseTitle = batch.Course?.Name ?? "",
                FromDate = rangeStart,
                ToDate = rangeEndInclusive,
                IsYesterdayOnly = isYesterdayOnly,
                TotalStudents = students.Count,
                Lectures = lecturVMs,
                TotalLecturesInPeriod = lectures.Count,
                OverallAttendanceRate = overallAttendanceRate,
                Homeworks = homeworkVMs,
                TotalHomeworksInPeriod = hwSets.Count,
                OverallSubmissionRate = overallSubmissionRate,
                OverallAverageScore = overallAverageScore,
                StudentsAbove60 = studentsAbove60,
                StudentsBelow60 = studentsBelow60,
                StudentsNotSubmitted = studentsNotSubmitted,
                CountAbove60 = studentsAbove60.Count,
                CountBelow60 = studentsBelow60.Count,
                CountNotSubmitted = studentsNotSubmitted.Count,
                CurriculumInstructors = curriculumInstructors
            };

            // ── مؤشر الخطورة لكل طالب ────────────────────────────────────
            foreach (var s in students)
            {
                var sRecs = hwStudentRecs.Where(r => r.StudentId == s.StudentID).ToList();
                var assigned = sRecs.Count;
                var submitted = sRecs.Count(r => r.IsSubmitted);
                var submRate = assigned > 0 ? submitted * 100.0 / assigned : 100.0;
                var scores = sRecs.Where(r => r.IsSubmitted && r.Score.HasValue).Select(r => r.Score!.Value).ToList();
                var avgSc = scores.Any() ? scores.Average() : 0;

                var sAtt = attendanceRecs.Where(a => a.StudentId == s.StudentID).ToList();
                var attRate = lectures.Count > 0 ? sAtt.Count(a => a.IsPresent) * 100.0 / lectures.Count : 100.0;

                double atRiskIndex = Math.Round(Math.Clamp(100 - (0.40 * submRate + 0.35 * avgSc + 0.25 * attRate), 0, 100), 1);
                string riskLevel, riskSeverity;
                if (atRiskIndex >= 60)      { riskLevel = "مرتفع";  riskSeverity = "rose"; }
                else if (atRiskIndex >= 30) { riskLevel = "متوسط";  riskSeverity = "amber"; }
                else                        { riskLevel = "منخفض";  riskSeverity = "emerald"; }

                vm.StudentsRisk.Add(new StudentRiskRowVM
                {
                    StudentId = s.StudentID,
                    FullName = s.FullName,
                    PhoneNumber = s.PhoneNumber,
                    AverageScore = Math.Round(avgSc, 1),
                    SubmissionRate = Math.Round(submRate, 1),
                    AttendanceRate = Math.Round(attRate, 1),
                    AtRiskIndex = atRiskIndex,
                    RiskLevel = riskLevel,
                    RiskSeverity = riskSeverity
                });
            }
            vm.StudentsRisk = vm.StudentsRisk.OrderByDescending(r => r.AtRiskIndex).ToList();

            // ── أفضل/أضعف 5 طلاب ─────────────────────────────────────────
            var allRows = studentsAbove60.Concat(studentsBelow60).Concat(studentsNotSubmitted).ToList();
            var ranked = allRows.Where(r => r.SubmittedCount > 0)
                .OrderByDescending(r => r.AverageScore)
                .ThenByDescending(r => r.AttendedCount)
                .ToList();
            vm.TopPerformers = ranked.Take(5).ToList();
            vm.BottomPerformers = ranked.Skip(Math.Max(0, ranked.Count - 5)).Reverse().ToList();

            // ── تفصيل المناهج/المدربين ────────────────────────────────────
            foreach (var ci in curriculumInstructors)
            {
                var ciHwVMs = homeworkVMs.Where(h => h.CurriculumId == ci.CurriculumId).ToList();
                var breakdown = new CurriculumBreakdownVM
                {
                    CurriculumId = ci.CurriculumId,
                    CurriculumTitle = ci.CurriculumTitle,
                    InstructorName = ci.InstructorName,
                    HomeworkCount = ciHwVMs.Count,
                    AverageScore = ciHwVMs.Any() ? Math.Round(ciHwVMs.Average(h => h.AverageScore), 1) : 0,
                    SubmissionRate = ciHwVMs.Any() ? Math.Round(ciHwVMs.Average(h => h.SubmissionRate), 1) : 0
                };
                vm.CurriculumBreakdown.Add(breakdown);
            }

            // ── بيانات الرسوم البيانية ────────────────────────────────────

            // 1) اتجاه الحضور
            vm.AttendanceTrendChart = new ChartDataVM
            {
                Labels = lecturVMs.Select(l => l.Title).ToList(),
                Series = new List<ChartSeriesVM> { new ChartSeriesVM { Name = "نسبة الحضور %", Values = lecturVMs.Select(l => l.AttendanceRate).ToList() } }
            };

            // 2) توزيع الدرجات
            var buckets = new int[6]; // 90-100, 75-89, 60-74, 40-59, 0-39, لم يسلّم
            foreach (var r in allRows)
            {
                if (r.SubmittedCount == 0) { buckets[5]++; continue; }
                if (r.AverageScore >= 90)      buckets[0]++;
                else if (r.AverageScore >= 75) buckets[1]++;
                else if (r.AverageScore >= 60) buckets[2]++;
                else if (r.AverageScore >= 40) buckets[3]++;
                else                           buckets[4]++;
            }
            vm.ScoreDistributionChart = new ChartDataVM
            {
                Labels = new List<string> { "90-100", "75-89", "60-74", "40-59", "0-39", "لم يسلّم" },
                Series = new List<ChartSeriesVM> { new ChartSeriesVM { Name = "عدد الطلاب", Values = buckets.Select(x => (double)x).ToList() } }
            };

            // 3) تسليم الواجبات ومتوسط الدرجات
            vm.HomeworkSubmissionChart = new ChartDataVM
            {
                Labels = homeworkVMs.Select(h => h.Title).ToList(),
                Series = new List<ChartSeriesVM>
                {
                    new ChartSeriesVM { Name = "نسبة التسليم %", Values = homeworkVMs.Select(h => h.SubmissionRate).ToList() },
                    new ChartSeriesVM { Name = "متوسط الدرجة %", Values = homeworkVMs.Select(h => h.AverageScore).ToList() }
                }
            };

            // 4) أداء المناهج
            var curriculaWithData = vm.CurriculumBreakdown.Where(c => c.HomeworkCount > 0).ToList();
            vm.CurriculumPerformanceChart = new ChartDataVM
            {
                Labels = curriculaWithData.Select(c => c.CurriculumTitle).ToList(),
                Series = new List<ChartSeriesVM>
                {
                    new ChartSeriesVM { Name = "متوسط الدرجة %", Values = curriculaWithData.Select(c => c.AverageScore).ToList() },
                    new ChartSeriesVM { Name = "نسبة التسليم %", Values = curriculaWithData.Select(c => c.SubmissionRate).ToList() }
                }
            };

            // ── الفترة السابقة ────────────────────────────────────────────
            var periodLengthDays = (rangeEndExclusive - rangeStart).Days;
            var prevRangeStart = rangeStart.AddDays(-periodLengthDays);
            var prevRangeEndExclusive = rangeStart;
            var prevRangeEndInclusive = prevRangeEndExclusive.AddDays(-1);

            var prevMetrics = await CalculatePeriodMetricsAsync(batchId, studentIds, students.Count, prevRangeStart, prevRangeEndExclusive);

            vm.PreviousFromDate = prevRangeStart;
            vm.PreviousToDate = prevRangeEndInclusive;
            vm.Comparison = new PeriodComparisonVM
            {
                AttendanceRate    = new KpiComparisonVM { CurrentValue = vm.OverallAttendanceRate,  PreviousValue = prevMetrics.OverallAttendanceRate },
                SubmissionRate    = new KpiComparisonVM { CurrentValue = vm.OverallSubmissionRate,  PreviousValue = prevMetrics.OverallSubmissionRate },
                AverageScore      = new KpiComparisonVM { CurrentValue = vm.OverallAverageScore,    PreviousValue = prevMetrics.OverallAverageScore },
                CountAbove60      = new KpiComparisonVM { CurrentValue = vm.CountAbove60,           PreviousValue = prevMetrics.CountAbove60 },
                CountBelow60      = new KpiComparisonVM { CurrentValue = vm.CountBelow60,           PreviousValue = prevMetrics.CountBelow60 },
                CountNotSubmitted = new KpiComparisonVM { CurrentValue = vm.CountNotSubmitted,      PreviousValue = prevMetrics.CountNotSubmitted },
            };

            // ── الملخص التنفيذي ───────────────────────────────────────────
            var compositeScore = (vm.OverallAverageScore + vm.OverallAttendanceRate + vm.OverallSubmissionRate) / 3.0;
            if (compositeScore >= 80)      { vm.OverallStatusLabel = "ممتاز";        vm.OverallStatusSeverity = "success";  }
            else if (compositeScore >= 65) { vm.OverallStatusLabel = "جيد";          vm.OverallStatusSeverity = "info";     }
            else if (compositeScore >= 50) { vm.OverallStatusLabel = "يحتاج تحسين"; vm.OverallStatusSeverity = "warning"; }
            else                           { vm.OverallStatusLabel = "حرج";          vm.OverallStatusSeverity = "critical"; }

            var trendWord = vm.Comparison.AverageScore.Trend == "up" ? "بتحسن"
                          : vm.Comparison.AverageScore.Trend == "down" ? "بتراجع"
                          : "باستقرار";

            vm.ExecutiveSummary =
                $"حالة الدفعة \"{vm.BatchName}\" خلال الفترة من {vm.FromDate:yyyy/MM/dd} إلى {vm.ToDate:yyyy/MM/dd} مصنّفة كـ \"{vm.OverallStatusLabel}\"، " +
                $"بمتوسط درجات {vm.OverallAverageScore:F0}% ونسبة حضور {vm.OverallAttendanceRate:F0}% ونسبة تسليم {vm.OverallSubmissionRate:F0}%، " +
                $"{trendWord} عن الفترة السابقة ({vm.Comparison.AverageScore.PreviousValue:F0}% → {vm.Comparison.AverageScore.CurrentValue:F0}%). " +
                (vm.CountNotSubmitted > 0
                    ? $"يوجد {vm.CountNotSubmitted} طالب لم يسلّموا واجباتهم ويحتاجون متابعة عاجلة."
                    : "جميع الطلاب سلّموا واجباتهم خلال هذه الفترة.");

            // ── التوصيات وخطة العمل ──────────────────────────────────────
            vm.Recommendations = _batchPerformanceRecommendationService.GenerateRecommendations(vm);
            vm.WeeklyActionPlan = _batchPerformanceRecommendationService.BuildWeeklyActionPlan(vm);

            if (print)
                return View("BatchPerformanceReport_Print", vm);

            return View("BatchPerformanceReport", vm);
        }

        private async Task<PeriodMetrics> CalculatePeriodMetricsAsync(
            int batchId, List<int> studentIds, int studentCount,
            DateTime rangeStart, DateTime rangeEndExclusive)
        {
            var lectures = await _context.Lecture
                .AsNoTracking()
                .Where(l => l.BatchId == batchId && l.Date >= rangeStart && l.Date < rangeEndExclusive)
                .Select(l => new { l.Id })
                .ToListAsync();

            var lectureIds = lectures.Select(l => l.Id).ToList();

            double overallAttendanceRate = 0;
            if (lectureIds.Any() && studentCount > 0)
            {
                var presentCount = await _context.AttendanceRecords
                    .AsNoTracking()
                    .Where(a => lectureIds.Contains(a.LectureId) && studentIds.Contains(a.StudentId) && a.IsPresent)
                    .CountAsync();
                overallAttendanceRate = Math.Round(presentCount * 100.0 / (lectureIds.Count * studentCount), 1);
            }

            var hwSets = await _context.HomeworkSets
                .AsNoTracking()
                .Where(hs => hs.BatchId == batchId && !hs.IsArchived
                             && hs.CreatedAt >= rangeStart && hs.CreatedAt < rangeEndExclusive)
                .Select(hs => new { hs.Id })
                .ToListAsync();

            var hwSetIds = hwSets.Select(hs => hs.Id).ToList();

            double overallSubmissionRate = 0;
            double overallAverageScore = 0;
            int countAbove60 = 0, countBelow60 = 0, countNotSubmitted = 0;

            if (hwSetIds.Any() && studentIds.Any())
            {
                var hwRecs = await _context.HomeworkSetStudents
                    .AsNoTracking()
                    .Where(r => hwSetIds.Contains(r.HomeworkSetId) && studentIds.Contains(r.StudentId))
                    .Select(r => new { r.StudentId, r.HomeworkSetId, r.IsSubmitted, r.Score })
                    .ToListAsync();

                var total = hwRecs.Count;
                var submitted = hwRecs.Count(r => r.IsSubmitted);
                overallSubmissionRate = total > 0 ? Math.Round(submitted * 100.0 / total, 1) : 0;
                var allScores = hwRecs.Where(r => r.IsSubmitted && r.Score.HasValue).Select(r => r.Score!.Value).ToList();
                overallAverageScore = allScores.Any() ? Math.Round(allScores.Average(), 1) : 0;

                foreach (var sid in studentIds)
                {
                    var sRecs = hwRecs.Where(r => r.StudentId == sid).ToList();
                    if (sRecs.Count == 0) continue;
                    var sSubmitted = sRecs.Count(r => r.IsSubmitted);
                    if (sSubmitted == 0) { countNotSubmitted++; continue; }
                    var sScores = sRecs.Where(r => r.IsSubmitted && r.Score.HasValue).Select(r => r.Score!.Value).ToList();
                    var sAvg = sScores.Any() ? sScores.Average() : 0;
                    if (sAvg >= 60) countAbove60++; else countBelow60++;
                }
            }

            return new PeriodMetrics
            {
                OverallAttendanceRate = overallAttendanceRate,
                OverallSubmissionRate = overallSubmissionRate,
                OverallAverageScore = overallAverageScore,
                CountAbove60 = countAbove60,
                CountBelow60 = countBelow60,
                CountNotSubmitted = countNotSubmitted
            };
        }

        private sealed class PeriodMetrics
        {
            public double OverallAttendanceRate { get; init; }
            public double OverallSubmissionRate { get; init; }
            public double OverallAverageScore { get; init; }
            public int CountAbove60 { get; init; }
            public int CountBelow60 { get; init; }
            public int CountNotSubmitted { get; init; }
        }

        private async Task<List<ApplicationUser>> GetArchiveApprovalCandidateUsersAsync()
        {
            var roleNames = new[] { "Admin", "Employee", "SuperAdmin" };
            var usersById = new Dictionary<string, ApplicationUser>();

            foreach (var roleName in roleNames)
            {
                var users = await _userManager.GetUsersInRoleAsync(roleName);
                foreach (var user in users.Where(x => x.IsActive))
                {
                    usersById[user.Id] = user;
                }
            }

            return usersById.Values.ToList();
        }
    }
}
