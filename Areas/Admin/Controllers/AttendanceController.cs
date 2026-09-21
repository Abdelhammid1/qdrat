using EFCore.BulkExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Security;
using QdratNew.Services.Admin;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Attendance;
using System.Security.Claims;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin,Owner,Employee,Developer,Employee")]
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAdminActivityLogger _activityLogger;
        private readonly IEmployeeBatchAccessService _batchAccess;

        public AttendanceController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAdminActivityLogger activityLogger,
            IEmployeeBatchAccessService batchAccess)
        {
            _context = context;
            _userManager = userManager;
            _activityLogger = activityLogger;
            _batchAccess = batchAccess;
        }

        // ─── Archive helpers ──────────────────────────────────────────
        private bool IsAttendanceArchiveOwner() =>
            User.IsInRole("Owner") || User.IsInRole("Developer");

        private string CurrentUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        private string CurrentUserName() =>
            User.Identity?.Name ?? "غير معروف";

        private async Task<bool> CanAccessArchivedAttendanceBatchAsync(int batchId)
        {
            var isArchived = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.IsArchived)
                .FirstOrDefaultAsync();

            if (!isArchived || IsAttendanceArchiveOwner())
                return true;

            var userId = CurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            return await _context.AttendanceBatchArchiveAccesses
                .AsNoTracking()
                .AnyAsync(x => x.BatchId == batchId &&
                               x.UserId == userId &&
                               x.IsActive);
        }

        private IActionResult AttendanceArchivedAccessDenied()
        {
            TempData["Error"] = "هذه الدفعة داخل الأرشيف ولا يمكن الوصول إليها إلا للمالك أو المبرمج أو مستخدم لديه موافقة صريحة.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LogAttendanceArchiveActivityAsync(int batchId, string actionType, string description)
        {
            await _activityLogger.LogAsync(
                actionType,
                description,
                CurrentUserId(),
                CurrentUserName(),
                null,
                null,
                batchId);
        }

        private async Task<List<ApplicationUser>> GetArchiveApprovalCandidateUsersAsync()
        {
            var roleNames = new[] { "Admin", "Employee", "SuperAdmin" };
            var usersById = new Dictionary<string, ApplicationUser>();

            foreach (var roleName in roleNames)
            {
                var users = await _userManager.GetUsersInRoleAsync(roleName);
                foreach (var user in users.Where(x => x.IsActive))
                    usersById[user.Id] = user;
            }

            return usersById.Values.ToList();
        }

        // =====================================================
        // Index = عرض الدفعات أولًا
        // =====================================================
        [HttpGet]
        [AdminPermission("Attendance", "Students")]
        public async Task<IActionResult> Index(int? instructorId)
        {
            var today = DateTime.Today;

            // صلاحية البروفايل (مُحققة بـ [AdminPermission]) كافية لرؤية الدفعات العادية
            // فلترة EmployeeBatchAccess تُطبق فقط على الأرشيف عبر CanAccessArchivedAttendanceBatchAsync
            List<int>? attendancePermittedBatchIds = null;

            var lecturesQuery = _context.Lecture
                .AsNoTracking()
                .Include(l => l.Batch)
                .ThenInclude(b => b.Course)
                .Include(l => l.Course)
                .Include(l => l.Section)
                .ThenInclude(s => s.Curriculum)
                .Include(l => l.Instructor)
                .Where(l => !l.Batch.IsArchived &&
                            (attendancePermittedBatchIds == null || attendancePermittedBatchIds.Contains(l.BatchId)))
                .AsQueryable();

            if (instructorId.HasValue)
            {
                lecturesQuery = lecturesQuery.Where(l => l.InstructorId == instructorId.Value);
            }

            var lectures = await lecturesQuery.ToListAsync();

            var archivedBatchCount = await _context.Lecture
                .Where(l => l.Batch.IsArchived)
                .Select(l => l.BatchId)
                .Distinct()
                .CountAsync();

            var activeEnrollments = await (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join s in _context.Students.AsNoTracking() on e.StudentID equals s.StudentID
                join u in _context.Users.AsNoTracking() on s.UserId equals u.Id
                where u.IsActive == true
                select new
                {
                    e.BatchId,
                    e.StudentID
                }
            ).ToListAsync();

            var attendanceRecords = await (
                from a in _context.AttendanceRecords.AsNoTracking()
                join l in _context.Lecture.AsNoTracking() on a.LectureId equals l.Id
                select new
                {
                    a.LectureId,
                    l.BatchId,
                    l.InstructorId,
                    a.StudentId,
                    a.IsPresent,
                    a.IsLateArrival,
                    a.HasEarlyLeavePermission
                }
            ).ToListAsync();

            if (instructorId.HasValue)
            {
                attendanceRecords = attendanceRecords
                    .Where(x => x.InstructorId == instructorId.Value)
                    .ToList();
            }

            var batchCards = lectures
                .GroupBy(l => l.BatchId)
                .Select(g =>
                {
                    var firstLecture = g.First();
                    var batchId = g.Key;

                    var batchLectures = g.ToList();
                    var batchAttendance = attendanceRecords
                        .Where(a => a.BatchId == batchId)
                        .ToList();

                    var totalStudents = activeEnrollments.Count(e => e.BatchId == batchId);
                    var totalLectures = batchLectures.Count;
                    var presentCount = batchAttendance.Count(a => a.IsPresent);

                    var instructorNames = batchLectures
                        .Where(x => x.Instructor != null)
                        .Select(x => x.Instructor.FullName)
                        .Distinct()
                        .ToList();

                    var instructorSummary = instructorNames.Count == 0
                        ? "غير محدد"
                        : instructorNames.Count == 1
                            ? instructorNames.First()
                            : $"{instructorNames.Count} مدربين";

                    var attendancePercentage =
                        totalStudents > 0 && totalLectures > 0
                            ? Math.Round((presentCount * 100.0) / (totalStudents * totalLectures), 1)
                            : 0;

                    return new AdminAttendanceBatchCardVM
                    {
                        BatchId = batchId,
                        BatchName = firstLecture.Batch != null ? firstLecture.Batch.Name : "غير محدد",
                        CourseTitle =
                            firstLecture.Batch != null && firstLecture.Batch.Course != null
                                ? firstLecture.Batch.Course.Name
                                : firstLecture.Course != null
                                    ? firstLecture.Course.Name
                                    : firstLecture.Section != null && firstLecture.Section.Curriculum != null
                                        ? firstLecture.Section.Curriculum.Title
                                        : "غير محدد",

                        IsArchived = firstLecture.Batch?.IsArchived ?? false,

                        TotalStudents = totalStudents,
                        TotalLectures = totalLectures,
                        TodayLectures = batchLectures.Count(x => x.Date.Date == today),

                        RecordedLectures = batchLectures.Count(l => batchAttendance.Any(a => a.LectureId == l.Id)),
                        PendingLectures = batchLectures.Count(l => !batchAttendance.Any(a => a.LectureId == l.Id)),

                        PresentCount = presentCount,
                        LateArrivalCount = batchAttendance.Count(a => a.IsLateArrival),
                        EarlyLeavePermissionCount = batchAttendance.Count(a => a.HasEarlyLeavePermission),

                        AttendancePercentage = attendancePercentage,
                        InstructorSummary = instructorSummary,
                        LastLectureDate = batchLectures
                            .OrderByDescending(x => x.Date)
                            .Select(x => (DateTime?)x.Date)
                            .FirstOrDefault()
                    };
                })
                .OrderByDescending(x => x.TodayLectures)
                .ThenByDescending(x => x.LastLectureDate)
                .ToList();

            var instructors = await _context.Instructors
                .AsNoTracking()
                .Where(i => i.IsActive)
                .OrderBy(i => i.FullName)
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = i.FullName,
                    Selected = instructorId.HasValue && i.Id == instructorId.Value
                })
                .ToListAsync();

            var allBatchesForArchive = batchCards
                .Select(x => new SelectListItem
                {
                    Value = x.BatchId.ToString(),
                    Text = $"{x.BatchName} — {x.CourseTitle}"
                })
                .ToList();

            var model = new AdminAttendanceIndexVM
            {
                InstructorId = instructorId,

                TotalBatches = batchCards.Count,
                TotalStudents = batchCards.Sum(x => x.TotalStudents),
                TotalLectures = batchCards.Sum(x => x.TotalLectures),
                TodayLectures = batchCards.Sum(x => x.TodayLectures),
                CompletedAttendanceLectures = batchCards.Sum(x => x.RecordedLectures),
                PendingAttendanceLectures = batchCards.Sum(x => x.PendingLectures),
                ArchivedBatchCount = archivedBatchCount,

                Instructors = instructors,
                AllBatchesForArchive = allBatchesForArchive,
                BatchCards = batchCards,
                ShowArchived = TempData["ShowArchived"] != null
            };

            return View(model);
        }

        // =====================================================
        // ArchivedAttendance = صفحة أرشيف الحضور المستقلة
        // =====================================================
        [HttpGet]
        [AdminPermission("Attendance", "Archive")]
        public async Task<IActionResult> ArchivedAttendance()
        {
            var today = DateTime.Today;

            var lectures = await _context.Lecture
                .AsNoTracking()
                .Include(l => l.Batch).ThenInclude(b => b.Course)
                .Include(l => l.Instructor)
                .Where(l => l.Batch.IsArchived)
                .ToListAsync();

            var batchIds = lectures.Select(l => l.BatchId).Distinct().ToList();

            var activeEnrollments = await (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join s in _context.Students.AsNoTracking() on e.StudentID equals s.StudentID
                join u in _context.Users.AsNoTracking() on s.UserId equals u.Id
                where u.IsActive == true && batchIds.Contains(e.BatchId)
                select new { e.BatchId, e.StudentID }
            ).ToListAsync();

            var attendanceRecords = await (
                from a in _context.AttendanceRecords.AsNoTracking()
                join l in _context.Lecture.AsNoTracking() on a.LectureId equals l.Id
                where batchIds.Contains(l.BatchId)
                select new { a.LectureId, l.BatchId, a.StudentId, a.IsPresent, a.IsLateArrival, a.HasEarlyLeavePermission }
            ).ToListAsync();

            var batchCards = lectures
                .GroupBy(l => l.BatchId)
                .Select(g =>
                {
                    var firstLecture = g.First();
                    var batchId = g.Key;
                    var batchLectures = g.ToList();
                    var batchAttendance = attendanceRecords.Where(a => a.BatchId == batchId).ToList();
                    var totalStudents = activeEnrollments.Count(e => e.BatchId == batchId);
                    var presentCount = batchAttendance.Count(a => a.IsPresent);
                    var totalLectures = batchLectures.Count;
                    var attendancePercentage = totalStudents > 0 && totalLectures > 0
                        ? Math.Round((presentCount * 100.0) / (totalStudents * totalLectures), 1)
                        : 0;
                    var instructorNames = batchLectures
                        .Where(x => x.Instructor != null)
                        .Select(x => x.Instructor.FullName).Distinct().ToList();

                    return new AdminAttendanceBatchCardVM
                    {
                        BatchId = batchId,
                        BatchName = firstLecture.Batch?.Name ?? "غير محدد",
                        CourseTitle = firstLecture.Batch?.Course?.Name ?? "غير محدد",
                        IsArchived = true,
                        TotalStudents = totalStudents,
                        TotalLectures = totalLectures,
                        RecordedLectures = batchLectures.Count(l => batchAttendance.Any(a => a.LectureId == l.Id)),
                        PendingLectures = batchLectures.Count(l => !batchAttendance.Any(a => a.LectureId == l.Id)),
                        PresentCount = presentCount,
                        LateArrivalCount = batchAttendance.Count(a => a.IsLateArrival),
                        EarlyLeavePermissionCount = batchAttendance.Count(a => a.HasEarlyLeavePermission),
                        AttendancePercentage = attendancePercentage,
                        InstructorSummary = instructorNames.Count == 0 ? "غير محدد"
                            : instructorNames.Count == 1 ? instructorNames.First()
                            : $"{instructorNames.Count} مدربين",
                        LastLectureDate = batchLectures.OrderByDescending(x => x.Date).Select(x => (DateTime?)x.Date).FirstOrDefault()
                    };
                })
                .OrderByDescending(x => x.LastLectureDate)
                .ToList();

            if (!IsAttendanceArchiveOwner())
            {
                var allowedBatchIds = await _context.AttendanceBatchArchiveAccesses
                    .Where(x => x.UserId == CurrentUserId() && x.IsActive)
                    .Select(x => x.BatchId)
                    .ToListAsync();
                batchCards = batchCards.Where(x => allowedBatchIds.Contains(x.BatchId)).ToList();
            }

            var vm = new AttendanceArchivedIndexVM
            {
                TotalArchivedBatches = batchCards.Count,
                TotalStudents = batchCards.Sum(x => x.TotalStudents),
                TotalLectures = batchCards.Sum(x => x.TotalLectures),
                AverageAttendancePercentage = batchCards.Any()
                    ? Math.Round(batchCards.Average(x => x.AttendancePercentage), 1) : 0,
                BatchCards = batchCards
            };

            return View(vm);
        }

        // =====================================================
        // Lectures = محاضرات دفعة واحدة
        // =====================================================
        [HttpGet]
        [AdminPermission("Attendance", "Students")]
        public async Task<IActionResult> Lectures(
            int batchId,
            int? instructorId,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            var today = DateTime.Today;

            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                return NotFound("لم يتم العثور على الدفعة.");

            if (batch.IsArchived && !await CanAccessArchivedAttendanceBatchAsync(batchId))
                return AttendanceArchivedAccessDenied();

            if (batch.IsArchived)
            {
                await LogAttendanceArchiveActivityAsync(batchId, "AttendanceArchiveView",
                    $"تم عرض محاضرات الدفعة المؤرشفة '{batch.Name}'.");
            }

            var lecturesQuery = _context.Lecture
                .AsNoTracking()
                .Include(l => l.Batch)
                .ThenInclude(b => b.Course)
                .Include(l => l.Course)
                .Include(l => l.Section)
                .ThenInclude(s => s.Curriculum)
                .Include(l => l.Instructor)
                .Where(l => l.BatchId == batchId);

            if (instructorId.HasValue)
            {
                lecturesQuery = lecturesQuery.Where(l => l.InstructorId == instructorId.Value);
            }

            if (dateFrom.HasValue)
            {
                var from = dateFrom.Value.Date;

                lecturesQuery = lecturesQuery.Where(l =>
                    l.Date.Year > from.Year ||
                    (l.Date.Year == from.Year && l.Date.Month > from.Month) ||
                    (l.Date.Year == from.Year && l.Date.Month == from.Month && l.Date.Day >= from.Day));
            }

            if (dateTo.HasValue)
            {
                var to = dateTo.Value.Date;

                lecturesQuery = lecturesQuery.Where(l =>
                    l.Date.Year < to.Year ||
                    (l.Date.Year == to.Year && l.Date.Month < to.Month) ||
                    (l.Date.Year == to.Year && l.Date.Month == to.Month && l.Date.Day <= to.Day));
            }

            var lectures = await lecturesQuery.ToListAsync();

            var studentsFromDb = await (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join s in _context.Students.AsNoTracking() on e.StudentID equals s.StudentID
                join u in _context.Users.AsNoTracking() on s.UserId equals u.Id
                where e.BatchId == batchId && u.IsActive == true
                orderby u.FullName
                select new
                {
                    StudentId = s.StudentID,
                    FullName = u.FullName,
                    PhoneNumber = s.PhoneNumber
                }
            ).ToListAsync();

            var totalStudents = studentsFromDb.Count;

            var attendanceRecords = await (
                from a in _context.AttendanceRecords.AsNoTracking()
                join l in _context.Lecture.AsNoTracking() on a.LectureId equals l.Id
                where l.BatchId == batchId
                select new
                {
                    a.LectureId,
                    l.InstructorId,
                    a.StudentId,
                    a.IsPresent,
                    a.IsLateArrival,
                    a.HasEarlyLeavePermission
                }
            ).ToListAsync();

            if (instructorId.HasValue)
            {
                attendanceRecords = attendanceRecords
                    .Where(x => x.InstructorId == instructorId.Value)
                    .ToList();
            }

            var lectureRows = lectures
                .OrderByDescending(l => l.Date.Date == today)
                .ThenBy(l => l.Date > today ? 0 : 1)
                .ThenByDescending(l => l.Date)
                .Select(l =>
                {
                    var lectureAttendance = attendanceRecords
                        .Where(a => a.LectureId == l.Id)
                        .ToList();

                    var presentCount = lectureAttendance.Count(a => a.IsPresent);
                    var absentCount = totalStudents - presentCount;

                    return new AdminLectureAttendanceRowVM
                    {
                        LectureId = l.Id,
                        Title = l.Title,
                        InstructorName = l.Instructor != null ? l.Instructor.FullName : "غير محدد",
                        BatchName = batch.Name,
                        CourseTitle =
                            batch.Course != null
                                ? batch.Course.Name
                                : l.Course != null
                                    ? l.Course.Name
                                    : l.Section != null && l.Section.Curriculum != null
                                        ? l.Section.Curriculum.Title
                                        : "غير محدد",
                        SectionTitle = l.Section != null ? l.Section.Title : "غير محدد",
                        Location = l.Location ?? "",
                        Date = l.Date,
                        IsToday = l.Date.Date == today,

                        TotalStudents = totalStudents,
                        PresentCount = presentCount,
                        AbsentCount = absentCount < 0 ? 0 : absentCount,
                        LateArrivalCount = lectureAttendance.Count(a => a.IsLateArrival),
                        EarlyLeavePermissionCount = lectureAttendance.Count(a => a.HasEarlyLeavePermission),

                        HasAttendance = lectureAttendance.Any(),
                        AttendancePercentage = totalStudents > 0
                            ? Math.Round((presentCount * 100.0) / totalStudents, 1)
                            : 0
                    };
                })
                .ToList();

            // ── واجبات الدفعة ──────────────────────────────────────────
            var hwSets = await _context.HomeworkSets
                .AsNoTracking()
                .Where(hs => hs.BatchId == batchId && !hs.IsArchived)
                .OrderByDescending(hs => hs.CreatedAt)
                .Select(hs => new { hs.Id, hs.Title, hs.CompletionTitle, hs.CreatedAt, hs.CurriculumId })
                .ToListAsync();

            var hwSetIds = hwSets.Select(hs => hs.Id).ToList();

            // ── مناهج الدورة (3-level cascade) ────────────────────────
            var courseCurriculumIds = await _context.CourseCurriculums
                .Where(cc => cc.CourseId == batch.CourseId)
                .Select(cc => cc.CurriculumId)
                .ToListAsync();

            var hwSetSectionCurriculums = await (
                from hss in _context.HomeworkSetSections
                join sec in _context.Sections on hss.SectionId equals sec.Id
                where hwSetIds.Contains(hss.HomeworkSetId)
                      && sec.CurriculumId > 0
                      && (!courseCurriculumIds.Any() || courseCurriculumIds.Contains(sec.CurriculumId))
                select new { hss.HomeworkSetId, sec.CurriculumId }
            ).Distinct().ToListAsync();

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

            var curriculumIds = hwCurriculumLookup.Values
                .Where(v => v.HasValue).Select(v => v!.Value).Distinct().ToList();

            var curriculumTitleMap = curriculumIds.Any()
                ? await _context.Curriculums
                    .Where(c => curriculumIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.Title)
                : new Dictionary<int, string>();

            var instructorCurrMap = new Dictionary<int, string>();
            if (curriculumIds.Any())
            {
                var instrRaw = await (
                    from icb in _context.InstructorCurriculumBatches
                    join ins in _context.Instructors on icb.InstructorId equals ins.Id
                    where icb.BatchId == batchId && curriculumIds.Contains(icb.CurriculumId)
                    select new { icb.CurriculumId, InstructorName = ins.FullName }
                ).ToListAsync();

                instructorCurrMap = instrRaw
                    .GroupBy(x => x.CurriculumId)
                    .ToDictionary(g => g.Key, g => g.First().InstructorName);
            }

            var attCurriculumInstructors = curriculumIds
                .Where(cid => curriculumTitleMap.ContainsKey(cid))
                .Select(cid => new AttCurriculumInstructorVM
                {
                    CurriculumId    = cid,
                    CurriculumTitle = curriculumTitleMap[cid],
                    InstructorName  = instructorCurrMap.GetValueOrDefault(cid, "")
                }).ToList();

            // ── سجلات الواجبات ───────────────────────────────────────
            var hwStudentRecs = await _context.HomeworkSetStudents
                .AsNoTracking()
                .Where(r => hwSetIds.Contains(r.HomeworkSetId))
                .Select(r => new
                {
                    r.HomeworkSetId, r.StudentId,
                    r.IsSubmitted, r.Score,
                    r.StudentReminderContacted, r.ParentContacted
                }).ToListAsync();

            // ── كل محاضرات الدفعة (بدون فلتر) لتقويم الطالب ──────────
            var allBatchLectures = await _context.Lecture
                .AsNoTracking()
                .Where(l => l.BatchId == batchId)
                .OrderBy(l => l.Date)
                .Select(l => new { l.Id, l.Title, l.Date })
                .ToListAsync();

            var allBatchLectureIds = allBatchLectures.Select(l => l.Id).ToList();

            var allBatchAttendance = await _context.AttendanceRecords
                .AsNoTracking()
                .Where(a => allBatchLectureIds.Contains(a.LectureId))
                .Select(a => new { a.StudentId, a.LectureId, a.IsPresent, a.IsLateArrival })
                .ToListAsync();

            var arCulture = new System.Globalization.CultureInfo("ar-SA");

            // ── بناء كروت الطلاب ─────────────────────────────────────
            var studentCards = studentsFromDb.Select(s =>
            {
                var sAtt = allBatchAttendance.Where(a => a.StudentId == s.StudentId).ToList();

                var lectureVMs = allBatchLectures.Select(l =>
                {
                    var rec = sAtt.FirstOrDefault(a => a.LectureId == l.Id);
                    return new AttStudentLectureVM
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
                    var rec = hwStudentRecs.FirstOrDefault(r => r.HomeworkSetId == hs.Id && r.StudentId == s.StudentId);
                    hwCurriculumLookup.TryGetValue(hs.Id, out var effCid);
                    return new AttStudentHwVM
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

                return new AttendanceBatchStudentCardVM
                {
                    StudentId       = s.StudentId,
                    FullName        = s.FullName ?? "غير محدد",
                    PhoneNumber     = s.PhoneNumber,
                    AttendedCount   = lectureVMs.Count(l => l.HasRecord && l.IsPresent),
                    TotalLectures   = allBatchLectures.Count,
                    SolvedHomeworks = hwVMs.Count(h => h.IsSolved),
                    TotalHomeworks  = hwSets.Count,
                    Lectures        = lectureVMs,
                    Homeworks       = hwVMs
                };
            }).ToList();

            var instructors = await _context.Instructors
                .AsNoTracking()
                .Where(i => i.IsActive)
                .OrderBy(i => i.FullName)
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = i.FullName,
                    Selected = instructorId.HasValue && i.Id == instructorId.Value
                })
                .ToListAsync();

            var model = new AdminBatchLecturesVM
            {
                BatchId = batch.Id,
                BatchName = batch.Name,
                CourseTitle = batch.Course != null ? batch.Course.Name : "غير محدد",

                InstructorId = instructorId,
                DateFrom = dateFrom,
                DateTo = dateTo,

                TotalStudents = totalStudents,
                TotalLectures = lectureRows.Count,
                TodayLectures = lectureRows.Count(x => x.IsToday),
                CompletedAttendanceLectures = lectureRows.Count(x => x.HasAttendance),
                PendingAttendanceLectures = lectureRows.Count(x => !x.HasAttendance),

                Instructors = instructors,
                Lectures = lectureRows,
                Students = studentCards,
                CurriculumInstructors = attCurriculumInstructors
            };

            ViewBag.IsArchived = batch.IsArchived;
            ViewBag.IsArchiveOwner = IsAttendanceArchiveOwner();

            return View(model);
        }

        // =====================================================
        // توافق مع روابط قديمة
        // =====================================================
        [HttpGet]
        [AdminPermission("Attendance", "Students")]
        public IActionResult AttendanceDashboard(int? instructorId)
        {
            return RedirectToAction(nameof(Index), new { instructorId });
        }

        [HttpGet]
        [AdminPermission("Attendance", "Students")]
        public IActionResult LectureList(int? batchId, int? instructorId)
        {
            if (batchId.HasValue)
                return RedirectToAction(nameof(Lectures), new { batchId = batchId.Value, instructorId });

            return RedirectToAction(nameof(Index), new { instructorId });
        }

        [HttpGet]
        [AdminPermission("Attendance", "Mark")]
        public IActionResult Mark(int id)
        {
            return RedirectToAction(nameof(Record), new { lectureId = id });
        }

        // =====================================================
        // Record GET = تسجيل حضور محاضرة واحدة
        // =====================================================
        [HttpGet]
        [AdminPermission("Attendance", "Mark")]
        public async Task<IActionResult> Record(int lectureId)
        {
            var lecture = await _context.Lecture
                .AsNoTracking()
                .Include(l => l.Batch)
                .ThenInclude(b => b.Course)
                .Include(l => l.Course)
                .Include(l => l.Section)
                .ThenInclude(s => s.Curriculum)
                .Include(l => l.Instructor)
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound("لم يتم العثور على المحاضرة.");

            if (lecture.Batch?.IsArchived == true)
            {
                if (!await CanAccessArchivedAttendanceBatchAsync(lecture.BatchId))
                    return AttendanceArchivedAccessDenied();

                await LogAttendanceArchiveActivityAsync(lecture.BatchId, "AttendanceArchiveView",
                    $"تم عرض تقرير محاضرة '{lecture.Title}' من الدفعة المؤرشفة '{lecture.Batch?.Name}'.");

                return RedirectToAction(nameof(LectureReport), new { lectureId });
            }

            var existingRecords = await _context.AttendanceRecords
                .AsNoTracking()
                .Where(a => a.LectureId == lecture.Id)
                .ToListAsync();

            var existingMap = existingRecords.ToDictionary(x => x.StudentId, x => x);

            var studentsFromDb = await (
                from e in _context.StudentBatchEnrollments
                join s in _context.Students on e.StudentID equals s.StudentID
                join u in _context.Users on s.UserId equals u.Id
                join br in _context.Branches on s.BranchId equals br.Id into JBR
                from br in JBR.DefaultIfEmpty()
                where e.BatchId == lecture.BatchId
                      && u.IsActive == true
                orderby u.FullName
                select new
                {
                    StudentId = s.StudentID,
                    StudentName = u.FullName,
                    NationalId = s.NationalID,
                    PhoneNumber = s.PhoneNumber,
                    BranchName = br != null ? br.Name : "غير محدد"
                }
            ).ToListAsync();

            var students = studentsFromDb
                .Select(s =>
                {
                    existingMap.TryGetValue(s.StudentId, out var record);

                    return new AdminAttendanceStudentRowVM
                    {
                        StudentId = s.StudentId,
                        StudentName = string.IsNullOrWhiteSpace(s.StudentName) ? "غير محدد" : s.StudentName,
                        NationalId = s.NationalId ?? "",
                        PhoneNumber = s.PhoneNumber ?? "",
                        BranchName = s.BranchName,

                        IsPresent = record?.IsPresent ?? false,
                        IsLateArrival = record?.IsLateArrival ?? false,
                        HasEarlyLeavePermission = record?.HasEarlyLeavePermission ?? false,
                        ActualArrivalTime = record?.ActualArrivalTime,
                        ActualDepartureTime = record?.ActualDepartureTime,
                        Notes = record?.Notes
                    };
                })
                .ToList();

            var model = BuildRecordModel(lecture, students);

            return View(model);
        }

        // =====================================================
        // Record POST = حفظ حضور المحاضرة
        // =====================================================
        // =====================================================
        // Record POST = حفظ حضور المحاضرة
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Attendance", "Mark")]
        public async Task<IActionResult> Record(AdminRecordAttendanceVM model)
        {
            if (model.Students == null || !model.Students.Any())
            {
                TempData["Error"] = "لم يتم إرسال بيانات الطلاب بشكل صحيح.";
                return RedirectToAction(nameof(Index));
            }

            var lecture = await _context.Lecture
                .AsNoTracking()
                .Include(l => l.Batch)
                .FirstOrDefaultAsync(l => l.Id == model.LectureId);

            if (lecture == null)
                return NotFound("لم يتم العثور على المحاضرة.");

            if (lecture.Batch?.IsArchived == true)
            {
                await LogAttendanceArchiveActivityAsync(
                    lecture.BatchId,
                    "AttendanceArchiveEditAttempt",
                    $"محاولة تعديل حضور محاضرة '{lecture.Title}' من الدفعة المؤرشفة '{lecture.Batch.Name}' — مرفوض."
                );

                TempData["Error"] = "لا يمكن تعديل سجلات الحضور لدفعة مؤرشفة.";
                return RedirectToAction(nameof(Lectures), new { batchId = lecture.BatchId });
            }

            var validStudents = await (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join s in _context.Students.AsNoTracking() on e.StudentID equals s.StudentID
                join u in _context.Users.AsNoTracking() on s.UserId equals u.Id
                where e.BatchId == lecture.BatchId
                      && u.IsActive == true
                select new
                {
                    StudentId = s.StudentID
                }
            ).ToListAsync();

            var validStudentIds = validStudents
                .Select(x => x.StudentId)
                .ToHashSet();

            var filteredStudents = model.Students
                .Where(x => validStudentIds.Contains(x.StudentId))
                .ToList();

            if (!filteredStudents.Any())
            {
                TempData["Error"] = "لا يوجد طلاب صالحون لتسجيل الحضور.";
                return RedirectToAction(nameof(Record), new { lectureId = model.LectureId });
            }

            var existingRecords = await _context.AttendanceRecords
                .Where(a => a.LectureId == model.LectureId)
                .ToListAsync();

            var isFirstAttendance = !existingRecords.Any();

            var existingMap = existingRecords
                .GroupBy(x => x.StudentId)
                .ToDictionary(g => g.Key, g => g.First());

            var now = DateTime.Now;
            var newRecords = new List<AttendanceRecord>();

            foreach (var student in filteredStudents)
            {
                var exists = existingMap.TryGetValue(student.StudentId, out var record);

                if (!exists || record == null)
                {
                    newRecords.Add(new AttendanceRecord
                    {
                        LectureId = model.LectureId,
                        StudentId = student.StudentId,
                        IsPresent = student.IsPresent,
                        IsLateArrival = student.IsLateArrival,
                        HasEarlyLeavePermission = student.HasEarlyLeavePermission,
                        ActualArrivalTime = student.ActualArrivalTime,
                        ActualDepartureTime = student.ActualDepartureTime,
                        EarlyLeavePermissionAt = student.HasEarlyLeavePermission ? now : null,
                        Notes = student.Notes,
                        RecordedAt = now
                    });
                }
                else
                {
                    record.IsPresent = student.IsPresent;
                    record.IsLateArrival = student.IsLateArrival;
                    record.HasEarlyLeavePermission = student.HasEarlyLeavePermission;
                    record.ActualArrivalTime = student.ActualArrivalTime;
                    record.ActualDepartureTime = student.ActualDepartureTime;
                    record.EarlyLeavePermissionAt = student.HasEarlyLeavePermission
                        ? record.EarlyLeavePermissionAt ?? now
                        : null;
                    record.Notes = student.Notes;
                    record.RecordedAt = now;
                }
            }

            if (newRecords.Any())
            {
                await _context.AttendanceRecords.AddRangeAsync(newRecords);
            }

            // تسجيل وقت البدء الفعلي عند أول تسجيل حضور للمحاضرة
            if (isFirstAttendance)
            {
                var lectureToUpdate = await _context.Lecture
                    .FirstOrDefaultAsync(l => l.Id == model.LectureId);

                if (lectureToUpdate != null && lectureToUpdate.ActualStartTime == null)
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    var roles = currentUser != null
                        ? await _userManager.GetRolesAsync(currentUser)
                        : new List<string>();

                    lectureToUpdate.ActualStartTime = DateTime.UtcNow;
                    lectureToUpdate.StartedByUserId = currentUser?.Id;
                    lectureToUpdate.StartedByRole = roles.FirstOrDefault() ?? "Admin";
                }
            }

            // مهم:
            // لا نستخدم BulkSaveChangesAsync هنا لأن تسجيل حضور محاضرة واحدة لا يحتاج Bulk
            // ولأن BulkSaveChangesAsync يتعارض مع SqlServerRetryingExecutionStrategy عند تفعيل EnableRetryOnFailure.
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حفظ الحضور والانصراف بنجاح.";
            return RedirectToAction(nameof(Lectures), new { batchId = lecture.BatchId });
        }
        // =====================================================
        // EndLecture POST = تسجيل وقت الانتهاء الفعلي يدوياً
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Attendance", "Mark")]
        public async Task<IActionResult> EndLecture(int lectureId)
        {
            var lecture = await _context.Lecture
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound("لم يتم العثور على المحاضرة.");

            if (lecture.ActualStartTime == null)
            {
                TempData["Error"] = "لا يمكن إنهاء محاضرة لم تبدأ بعد.";
                return RedirectToAction(nameof(Record), new { lectureId });
            }

            // first one wins — لا يُعاد الكتابة إذا سُجِّل وقت الانتهاء مسبقاً
            if (lecture.ActualEndTime != null)
            {
                TempData["Info"] = "تم تسجيل وقت انتهاء المحاضرة مسبقاً ولا يمكن تغييره.";
                return RedirectToAction(nameof(Record), new { lectureId });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var roles = currentUser != null
                ? await _userManager.GetRolesAsync(currentUser)
                : new List<string>();

            lecture.ActualEndTime = DateTime.UtcNow;
            lecture.EndedByUserId = currentUser?.Id;
            lecture.EndedByRole = roles.FirstOrDefault() ?? "Admin";

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم تسجيل وقت انتهاء المحاضرة الفعلي بنجاح.";
            return RedirectToAction(nameof(Record), new { lectureId });
        }

        // =====================================================
        // LectureReport = تقرير محاضرة واحدة
        // =====================================================
        [HttpGet]
        [AdminPermission("Attendance", "Students")]
        public async Task<IActionResult> LectureReport(int lectureId)
        {
            var lecture = await _context.Lecture
                .AsNoTracking()
                .Include(l => l.Batch)
                .ThenInclude(b => b.Course)
                .Include(l => l.Course)
                .Include(l => l.Section)
                .ThenInclude(s => s.Curriculum)
                .Include(l => l.Instructor)
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound("لم يتم العثور على المحاضرة.");

            var studentsFromDb = await (
                from e in _context.StudentBatchEnrollments
                join s in _context.Students on e.StudentID equals s.StudentID
                join u in _context.Users on s.UserId equals u.Id
                join br in _context.Branches on s.BranchId equals br.Id into JBR
                from br in JBR.DefaultIfEmpty()
                where e.BatchId == lecture.BatchId
                      && u.IsActive == true
                orderby u.FullName
                select new
                {
                    StudentId = s.StudentID,
                    StudentName = u.FullName,
                    NationalId = s.NationalID,
                    PhoneNumber = s.PhoneNumber,
                    BranchName = br != null ? br.Name : "غير محدد"
                }
            ).ToListAsync();

            var attendance = await _context.AttendanceRecords
                .AsNoTracking()
                .Where(a => a.LectureId == lecture.Id)
                .ToListAsync();

            var map = attendance.ToDictionary(x => x.StudentId, x => x);

            var students = studentsFromDb
                .Select(s =>
                {
                    map.TryGetValue(s.StudentId, out var record);

                    return new AdminAttendanceStudentRowVM
                    {
                        StudentId = s.StudentId,
                        StudentName = string.IsNullOrWhiteSpace(s.StudentName) ? "غير محدد" : s.StudentName,
                        NationalId = s.NationalId ?? "",
                        PhoneNumber = s.PhoneNumber ?? "",
                        BranchName = s.BranchName,

                        IsPresent = record?.IsPresent ?? false,
                        IsLateArrival = record?.IsLateArrival ?? false,
                        HasEarlyLeavePermission = record?.HasEarlyLeavePermission ?? false,
                        ActualArrivalTime = record?.ActualArrivalTime,
                        ActualDepartureTime = record?.ActualDepartureTime,
                        Notes = record?.Notes
                    };
                })
                .ToList();

            var model = BuildRecordModel(lecture, students);

            return View(model);
        }

        // =====================================================
        // ArchiveBatches / RestoreBatches
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Attendance", "Archive")]
        public async Task<IActionResult> ArchiveBatches(List<int> selectedBatchIds)
        {
            if (selectedBatchIds == null || !selectedBatchIds.Any())
            {
                TempData["Error"] = "لم تحدد أي دفعة.";
                TempData["ShowArchived"] = "1";
                return RedirectToAction(nameof(Index));
            }

            var batches = await _context.Batches
                .Where(b => selectedBatchIds.Contains(b.Id) && !b.IsArchived)
                .ToListAsync();

            foreach (var batch in batches)
            {
                batch.IsArchived = true;
                await LogAttendanceArchiveActivityAsync(batch.Id, "AttendanceBatchArchived",
                    $"تم أرشفة دفعة الحضور '{batch.Name}'.");
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم أرشفة {batches.Count} دفعة بنجاح.";
            TempData["ShowArchived"] = "1";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Attendance", "Archive")]
        public async Task<IActionResult> RestoreBatches(List<int> selectedBatchIds)
        {
            if (selectedBatchIds == null || !selectedBatchIds.Any())
            {
                TempData["Error"] = "لم تحدد أي دفعة.";
                TempData["ShowArchived"] = "1";
                return RedirectToAction(nameof(Index));
            }

            var batches = await _context.Batches
                .Where(b => selectedBatchIds.Contains(b.Id) && b.IsArchived)
                .ToListAsync();

            foreach (var batch in batches)
            {
                batch.IsArchived = false;
                await LogAttendanceArchiveActivityAsync(batch.Id, "AttendanceBatchRestored",
                    $"تم إخراج دفعة الحضور '{batch.Name}' من الأرشيف.");
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم إخراج {batches.Count} دفعة من الأرشيف بنجاح.";
            TempData["ShowArchived"] = "1";
            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // ManageAttendanceArchiveAccess (Owner/Developer only)
        // =====================================================
        [HttpGet]
        [AdminPermission("Attendance", "Archive")]
        public async Task<IActionResult> ManageArchiveAccess(int batchId)
        {
            if (!IsAttendanceArchiveOwner())
                return Forbid();

            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null) return NotFound();

            if (!batch.IsArchived)
            {
                TempData["Error"] = "إدارة موافقات الأرشيف متاحة للدفعات المؤرشفة فقط.";
                return RedirectToAction(nameof(Index));
            }

            var users = await GetArchiveApprovalCandidateUsersAsync();
            var activeUserIds = (await _context.AttendanceBatchArchiveAccesses
                .AsNoTracking()
                .Where(x => x.BatchId == batchId && x.IsActive)
                .Select(x => x.UserId)
                .ToListAsync()).ToHashSet();

            var items = new List<AttendanceArchiveUserAccessItem>();
            foreach (var user in users.OrderBy(x => x.FullName ?? x.UserName))
            {
                var roles = await _userManager.GetRolesAsync(user);
                items.Add(new AttendanceArchiveUserAccessItem
                {
                    UserId      = user.Id,
                    DisplayName = user.FullName ?? user.UserName ?? user.Email ?? user.Id,
                    Email       = user.Email ?? string.Empty,
                    Roles       = string.Join("، ", roles),
                    IsAllowed   = activeUserIds.Contains(user.Id)
                });
            }

            var model = new AttendanceBatchArchiveAccessVM
            {
                BatchId     = batch.Id,
                BatchName   = batch.Name,
                CourseTitle = batch.Course?.Name ?? string.Empty,
                Users       = items
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Attendance", "Archive")]
        public async Task<IActionResult> UpdateArchiveAccess(int batchId, List<string> allowedUserIds)
        {
            if (!IsAttendanceArchiveOwner())
                return Forbid();

            var batch = await _context.Batches.FirstOrDefaultAsync(b => b.Id == batchId);
            if (batch == null) return NotFound();

            if (!batch.IsArchived)
            {
                TempData["Error"] = "لا يمكن تعديل موافقات الأرشيف لدفعة غير مؤرشفة.";
                return RedirectToAction(nameof(Index));
            }

            allowedUserIds ??= new List<string>();
            var allowedSet = allowedUserIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct().ToHashSet();

            var candidateUsers    = await GetArchiveApprovalCandidateUsersAsync();
            var candidateUserIds  = candidateUsers.Select(x => x.Id).ToHashSet();
            allowedSet.RemoveWhere(x => !candidateUserIds.Contains(x));

            var existing        = await _context.AttendanceBatchArchiveAccesses
                .Where(x => x.BatchId == batchId).ToListAsync();
            var existingUserIds = existing.Select(x => x.UserId).ToHashSet();
            var now             = DateTime.UtcNow;
            var currentUserId   = CurrentUserId();

            foreach (var access in existing)
                access.IsActive = allowedSet.Contains(access.UserId);

            foreach (var userId in allowedSet.Where(x => !existingUserIds.Contains(x)))
            {
                _context.AttendanceBatchArchiveAccesses.Add(new AttendanceBatchArchiveAccess
                {
                    BatchId         = batchId,
                    UserId          = userId,
                    GrantedByUserId = currentUserId,
                    GrantedAt       = now,
                    IsActive        = true
                });
            }

            await _context.SaveChangesAsync();

            await LogAttendanceArchiveActivityAsync(batchId, "AttendanceArchiveAccessUpdated",
                $"تم تعديل موافقات الوصول لحضور الدفعة المؤرشفة '{batch.Name}'. عدد المصرح لهم: {allowedSet.Count}.");

            TempData["Success"] = "تم تحديث موافقات الوصول للأرشيف.";
            return RedirectToAction(nameof(ManageArchiveAccess), new { batchId });
        }

        // =====================================================
        // AttendanceArchiveHistory
        // =====================================================
        [HttpGet]
        [AdminPermission("Attendance", "Archive")]
        public async Task<IActionResult> ArchiveHistory(int batchId)
        {
            if (!await CanAccessArchivedAttendanceBatchAsync(batchId))
                return AttendanceArchivedAccessDenied();

            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null) return NotFound();

            var items = await _context.AdminActivityLogs
                .AsNoTracking()
                .Where(x => x.BatchId == batchId &&
                             x.ActionType.StartsWith("Attendance"))
                .OrderByDescending(x => x.Timestamp)
                .Select(x => new AttendanceArchiveHistoryItem
                {
                    Timestamp   = x.Timestamp,
                    AdminName   = x.AdminName,
                    ActionType  = x.ActionType,
                    Description = x.Description
                })
                .ToListAsync();

            var model = new AttendanceBatchArchiveHistoryVM
            {
                BatchId     = batch.Id,
                BatchName   = batch.Name,
                CourseTitle = batch.Course?.Name ?? string.Empty,
                Items       = items
            };

            return View(model);
        }

        // =====================================================
        // Helper
        // =====================================================
        private static AdminRecordAttendanceVM BuildRecordModel(
            Lecture lecture,
            List<AdminAttendanceStudentRowVM> students)
        {
            return new AdminRecordAttendanceVM
            {
                LectureId = lecture.Id,
                BatchId = lecture.BatchId,

                LectureTitle = lecture.Title,
                InstructorName = lecture.Instructor != null ? lecture.Instructor.FullName : "غير محدد",
                BatchName = lecture.Batch != null ? lecture.Batch.Name : "غير محدد",
                CourseTitle =
                    lecture.Batch != null && lecture.Batch.Course != null
                        ? lecture.Batch.Course.Name
                        : lecture.Course != null
                            ? lecture.Course.Name
                            : lecture.Section != null && lecture.Section.Curriculum != null
                                ? lecture.Section.Curriculum.Title
                                : "غير محدد",
                SectionTitle = lecture.Section != null ? lecture.Section.Title : "غير محدد",
                Location = lecture.Location ?? "",
                LectureDate = lecture.Date,
                Students = students
            };
        }
    }
}