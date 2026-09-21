using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Security;
using QdratNew.Services.Admin;
using QdratNew.Services.Batches;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Batch;
using System.Linq;
using System.Security.Claims;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee,DataEntry")]
    public class BatchesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBatchLectureAutoGenerationService _batchLectureAutoGenerationService;
        private readonly IEmployeeBatchAccessService _batchAccess;
        private readonly UserManager<ApplicationUser> _userManager;

        public BatchesController(
            ApplicationDbContext context,
            IBatchLectureAutoGenerationService batchLectureAutoGenerationService,
            IEmployeeBatchAccessService batchAccess,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _batchLectureAutoGenerationService = batchLectureAutoGenerationService;
            _batchAccess = batchAccess;
            _userManager = userManager;
        }

        // ─── Archive helpers ──────────────────────────────────────────
        private bool IsBatchesArchiveOwner() =>
            User.IsInRole("Owner") || User.IsInRole("Developer");

        private string CurrentUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        private async Task<bool> CanAccessArchivedBatchAsync(int batchId)
        {
            var isArchived = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.IsArchived)
                .FirstOrDefaultAsync();

            if (!isArchived || IsBatchesArchiveOwner())
                return true;

            var userId = CurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            return await _context.BatchesArchiveAccesses
                .AsNoTracking()
                .AnyAsync(x => x.BatchId == batchId &&
                               x.UserId == userId &&
                               x.IsActive);
        }

        private IActionResult BatchArchiveAccessDenied()
        {
            TempData["ErrorMessage"] = "هذه الدفعة داخل الأرشيف ولا يمكن الوصول إليها إلا للمالك أو المبرمج أو مستخدم لديه موافقة صريحة.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<ApplicationUser>> GetArchiveAccessCandidatesAsync()
        {
            var roleNames = new[] { "Admin", "Employee", "SuperAdmin", "DataEntry" };
            var usersById = new Dictionary<string, ApplicationUser>();
            foreach (var roleName in roleNames)
            {
                var users = await _userManager.GetUsersInRoleAsync(roleName);
                foreach (var user in users.Where(x => x.IsActive))
                    usersById[user.Id] = user;
            }
            return usersById.Values.ToList();
        }

        private List<SelectListItem> GetCoursesSelectList()
        {
            return _context.Courses
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToList();
        }


        [AdminPermission("Batches", "Read")]
        public async Task<IActionResult> Index(string view = "active")
        {
            DateTime today = DateTime.Today;

            int activeCount = await _context.Batches
                .AsNoTracking()
                .CountAsync(b => !b.IsDeleted && !b.IsArchived && (!b.EndDate.HasValue || b.EndDate.Value >= today));

            int graduatedCount = await _context.Batches
                .AsNoTracking()
                .CountAsync(b => !b.IsDeleted && !b.IsArchived && b.EndDate.HasValue && b.EndDate.Value < today);

            var query = _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .Where(b => !b.IsDeleted)
                .AsQueryable();

            if (string.IsNullOrWhiteSpace(view))
            {
                view = "active";
            }

            if (view == "archived")
            {
                query = query.Where(b => b.IsArchived);
            }
            else if (view == "graduated")
            {
                query = query.Where(b => !b.IsArchived && b.EndDate.HasValue && b.EndDate.Value < today);
            }
            else if (view == "all")
            {
                query = query.Where(b => !b.IsArchived);
            }
            else
            {
                view = "active";
                query = query.Where(b => !b.IsArchived && (!b.EndDate.HasValue || b.EndDate.Value >= today));
            }

            // فلترة الدفعات بناءً على صلاحية ViewBatch للموظفين/الأدمن/السوبرأدمن
            // يُستثنى من الفلتر: المالك/المبرمج، وأي مستخدم يملك صلاحية "Batches" كاملة (غير Custom) من بروفايل الصلاحيات
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var hasFullAccess = _batchAccess.IsPrivilegedUser(User) ||
                                 await _batchAccess.HasFullBatchesAccessAsync(userId);

            if (!hasFullAccess)
            {
                var permittedIds = await _batchAccess.GetPermittedBatchIdsAsync(userId, InstructorBatchFeature.ViewBatch);
                query = query.Where(b => permittedIds.Contains(b.Id));
            }

            var batchEntities = await query
                .OrderByDescending(b => b.Id)
                .ToListAsync();

            var batchIds = batchEntities.Select(b => b.Id).ToList();
            var batchIdSet = batchIds.ToHashSet();

            var lectureRows = await _context.Lecture
                .AsNoTracking()
                .Select(l => new { l.BatchId })
                .ToListAsync();

            var studentRows = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Select(e => new { e.BatchId })
                .ToListAsync();

            var lectureCounts = lectureRows
                .Where(l => batchIdSet.Contains(l.BatchId))
                .GroupBy(l => l.BatchId)
                .ToDictionary(g => g.Key, g => g.Count());

            var studentCounts = studentRows
                .Where(e => batchIdSet.Contains(e.BatchId))
                .GroupBy(e => e.BatchId)
                .ToDictionary(g => g.Key, g => g.Count());

            int archivedCount = await _context.Batches
                .AsNoTracking()
                .CountAsync(b => !b.IsDeleted && b.IsArchived);

            var allNonArchivedBatchIds = await _context.Batches
                .AsNoTracking()
                .Where(b => !b.IsDeleted && !b.IsArchived)
                .Select(b => b.Id)
                .ToListAsync();

            var allNonArchivedBatchIdSet = allNonArchivedBatchIds.ToHashSet();
            var nonArchivedWithLecturesIds = lectureRows
                .Where(l => allNonArchivedBatchIdSet.Contains(l.BatchId))
                .Select(l => l.BatchId)
                .Distinct()
                .ToList();

            var batches = batchEntities
                .Select(b => new BatchViewModel
                {
                    Id = b.Id,
                    Name = b.Name,
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    CourseId = b.CourseId,
                    BranchId = b.BranchId,
                    Gender = b.Gender,
                    IsArchived = b.IsArchived,
                    LecturesCount = lectureCounts.TryGetValue(b.Id, out var lecturesCount) ? lecturesCount : 0,
                    StudentsCount = studentCounts.TryGetValue(b.Id, out var studentsCount) ? studentsCount : 0,

                    Courses = new List<SelectListItem>
                    {
                new SelectListItem
                {
                    Value = b.Course != null ? b.Course.Id.ToString() : b.CourseId.ToString(),
                    Text = b.Course != null ? b.Course.Name : "غير محدد"
                }
                    }
                })
                .ToList();

            ViewBag.CurrentBatchView = view;
            ViewBag.ActiveBatchesCount = activeCount;
            ViewBag.GraduatedBatchesCount = graduatedCount;
            ViewBag.ArchivedBatchesCount = archivedCount;
            ViewBag.AllBatchesCount = activeCount + graduatedCount;
            ViewBag.BatchesWithLecturesCount = nonArchivedWithLecturesIds.Count;
            ViewBag.BatchesWithoutLecturesCount = allNonArchivedBatchIds.Count - nonArchivedWithLecturesIds.Count;

            return View(batches);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Batches", "Archive")]
        public async Task<IActionResult> ArchiveBatchesByIds(List<int> selectedBatchIds)
        {
            if (selectedBatchIds == null || selectedBatchIds.Count == 0)
            {
                TempData["ErrorMessage"] = "من فضلك اختر دفعة واحدة على الأقل للأرشفة.";
                return RedirectToAction(nameof(Index));
            }

            var selectedIds = selectedBatchIds.ToHashSet();
            var batches = (await _context.Batches
                .Where(b => !b.IsDeleted && !b.IsArchived)
                .ToListAsync())
                .Where(b => selectedIds.Contains(b.Id))
                .ToList();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            foreach (var batch in batches)
            {
                batch.IsArchived = true;
                batch.ArchivedAt = DateTime.Now;
                batch.ArchivedByUserId = userId;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"تم نقل {batches.Count} دفعة إلى الأرشيف.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Batches", "Archive")]
        public async Task<IActionResult> RestoreBatchesByIds(List<int> selectedBatchIds, bool returnToArchive = false)
        {
            if (selectedBatchIds == null || selectedBatchIds.Count == 0)
            {
                TempData["ErrorMessage"] = "من فضلك اختر دفعة واحدة على الأقل للاسترجاع.";
                return returnToArchive
                    ? RedirectToAction(nameof(Archive))
                    : RedirectToAction(nameof(Index), new { view = "archived" });
            }

            var selectedIds = selectedBatchIds.ToHashSet();
            var batches = (await _context.Batches
                .Where(b => !b.IsDeleted && b.IsArchived)
                .ToListAsync())
                .Where(b => selectedIds.Contains(b.Id))
                .ToList();

            foreach (var batch in batches)
            {
                batch.IsArchived = false;
                batch.ArchivedAt = null;
                batch.ArchivedByUserId = null;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"تم إخراج {batches.Count} دفعة من الأرشيف.";

            return returnToArchive
                ? RedirectToAction(nameof(Archive))
                : RedirectToAction(nameof(Index), new { view = "archived" });
        }



        [AdminPermission("Batches", "Archive")]
        public async Task<IActionResult> Archive()
        {
            var archivedBatches = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .Where(b => !b.IsDeleted && b.IsArchived)
                .OrderByDescending(b => b.ArchivedAt)
                .ToListAsync();

            var batchIds = archivedBatches.Select(b => b.Id).ToHashSet();

            var archivedByUserIds = archivedBatches
                .Where(b => b.ArchivedByUserId != null)
                .Select(b => b.ArchivedByUserId!)
                .Distinct()
                .ToList();

            var userNames = await _context.Users
                .AsNoTracking()
                .Where(u => archivedByUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? "غير معروف");

            var lectureCounts = await _context.Lecture
                .AsNoTracking()
                .Where(l => batchIds.Contains(l.BatchId))
                .GroupBy(l => l.BatchId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var studentCounts = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => batchIds.Contains(e.BatchId))
                .GroupBy(e => e.BatchId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var viewModels = archivedBatches.Select(b => new BatchArchiveViewModel
            {
                Id = b.Id,
                Name = b.Name,
                CourseName = b.Course?.Name ?? "غير محدد",
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                Gender = b.Gender.ToString(),
                StudentsCount = studentCounts.TryGetValue(b.Id, out var sc) ? sc : 0,
                LecturesCount = lectureCounts.TryGetValue(b.Id, out var lc) ? lc : 0,
                ArchivedAt = b.ArchivedAt,
                ArchivedByUserName = b.ArchivedByUserId != null && userNames.TryGetValue(b.ArchivedByUserId, out var uname)
                    ? uname : null
            }).ToList();

            ViewBag.TotalArchived = viewModels.Count;
            ViewBag.LastArchivedAt = viewModels.FirstOrDefault()?.ArchivedAt;

            return View(viewModels);
        }

        // ─── إدارة طلاب دفعة مؤرشفة (Owner/Developer فقط) ──────────────
        [HttpGet]
        public async Task<IActionResult> Students(int batchId)
        {
            if (!IsBatchesArchiveOwner())
                return BatchArchiveAccessDenied();

            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .Where(b => b.Id == batchId && !b.IsDeleted && b.IsArchived)
                .FirstOrDefaultAsync();

            if (batch == null)
            {
                TempData["ErrorMessage"] = "هذه الدفعة غير موجودة أو غير مؤرشفة.";
                return RedirectToAction(nameof(Archive));
            }

            var students = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == batchId)
                .Join(_context.Students,
                    e => e.StudentID,
                    s => s.StudentID,
                    (e, s) => new { e, s })
                .OrderBy(x => x.s.FullName)
                .ToListAsync();

            var studentIds = students.Select(x => x.s.StudentID).ToList();

            var otherEnrollmentCounts = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => studentIds.Contains(e.StudentID) && e.BatchId != batchId)
                .GroupBy(e => e.StudentID)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var revokedByUserIds = students
                .Where(x => x.s.AccessRevokedByUserId != null)
                .Select(x => x.s.AccessRevokedByUserId!)
                .Distinct()
                .ToList();

            var userNames = await _context.Users
                .AsNoTracking()
                .Where(u => revokedByUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? "غير معروف");

            var rows = students.Select(x => new BatchStudentAccessRowViewModel
            {
                StudentId = x.s.StudentID,
                UserId = x.s.UserId,
                FullName = x.s.FullName ?? "",
                NationalID = x.s.NationalID,
                Phone = x.s.PhoneNumber,
                School = x.s.School,
                Level = x.s.Level,
                EnrollmentStatus = x.e.Status,
                EnrolledAt = x.e.EnrolledAt,
                HasActiveEnrollment = otherEnrollmentCounts.ContainsKey(x.s.StudentID),
                CanAccessStudentArea = x.s.CanAccessStudentArea,
                AccessRevokedAt = x.s.AccessRevokedAt,
                AccessRevokedByUserName = x.s.AccessRevokedByUserId != null && userNames.TryGetValue(x.s.AccessRevokedByUserId, out var uname)
                    ? uname : null
            }).ToList();

            ViewBag.BatchId = batch.Id;
            ViewBag.BatchName = batch.Name;
            ViewBag.CourseName = batch.Course?.Name ?? "غير محدد";
            ViewBag.ArchivedAt = batch.ArchivedAt;

            return View(rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStudentAccess(List<int> studentIds, bool allow, int batchId)
        {
            if (!IsBatchesArchiveOwner())
                return BatchArchiveAccessDenied();

            if (studentIds == null || studentIds.Count == 0)
            {
                TempData["ErrorMessage"] = "من فضلك اختر طالباً واحداً على الأقل.";
                return RedirectToAction(nameof(Students), new { batchId });
            }

            var idsSet = studentIds.ToHashSet();
            var students = await _context.Students
                .Where(s => idsSet.Contains(s.StudentID))
                .ToListAsync();

            var userId = CurrentUserId();

            foreach (var student in students)
            {
                student.CanAccessStudentArea = allow;
                student.AccessRevokedAt = allow ? null : DateTime.Now;
                student.AccessRevokedByUserId = allow ? null : userId;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = allow
                ? $"تم منح {students.Count} طالب إمكانية الدخول لاريا الطالب."
                : $"تم سلب إمكانية الدخول عن {students.Count} طالب.";

            return RedirectToAction(nameof(Students), new { batchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBatchStudentsAccess(int batchId, bool allow)
        {
            if (!IsBatchesArchiveOwner())
                return BatchArchiveAccessDenied();

            var studentIdsInBatch = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == batchId)
                .Select(e => e.StudentID)
                .ToListAsync();

            var otherEnrollmentStudentIds = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => studentIdsInBatch.Contains(e.StudentID) && e.BatchId != batchId)
                .Select(e => e.StudentID)
                .Distinct()
                .ToListAsync();

            var soloEnrollmentIds = studentIdsInBatch.Except(otherEnrollmentStudentIds).ToHashSet();

            var students = await _context.Students
                .Where(s => soloEnrollmentIds.Contains(s.StudentID))
                .ToListAsync();

            var userId = CurrentUserId();

            foreach (var student in students)
            {
                student.CanAccessStudentArea = allow;
                student.AccessRevokedAt = allow ? null : DateTime.Now;
                student.AccessRevokedByUserId = allow ? null : userId;
            }

            await _context.SaveChangesAsync();

            var excludedCount = otherEnrollmentStudentIds.Count;
            TempData["SuccessMessage"] = allow
                ? $"تم منح الدخول لـ {students.Count} طالب."
                : $"تم سلب الدخول عن {students.Count} طالب" +
                  (excludedCount > 0 ? $" (تم استثناء {excludedCount} طالب لديهم تسجيل في دفعات أخرى)." : ".");

            return RedirectToAction(nameof(Students), new { batchId });
        }

        [AdminPermission("Batches", "Create")]
        public IActionResult Create()
        {
            var model = new BatchViewModel();
            PopulateDropdowns(model);
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Batches", "Create")]
        public async Task<IActionResult> Create(BatchViewModel model)
        {
            if (model.AutoGenerateLectures && !model.FirstLectureStartDateTime.HasValue)
            {
                ModelState.AddModelError(nameof(model.FirstLectureStartDateTime), "تاريخ ووقت بداية أول محاضرة مطلوب عند تفعيل إنشاء المحاضرات تلقائيًا.");
            }

            if (!ModelState.IsValid)
            {
                PopulateDropdowns(model); // ✅ إعادة تعبئة الدروب داون عند الخطأ
                return View(model);
            }

            var batch = new Batch
            {
                Name = model.Name,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                CourseId = model.CourseId,
                BranchId = model.BranchId,
                Gender = model.Gender
            };

            var executionStrategy = _context.Database.CreateExecutionStrategy();

            try
            {
                await executionStrategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    _context.Batches.Add(batch);
                    await _context.SaveChangesAsync();

                    if (model.AutoGenerateLectures && model.FirstLectureStartDateTime.HasValue)
                    {
                        await _batchLectureAutoGenerationService.GenerateForBatchAsync(
                            batch.Id,
                            batch.CourseId,
                            model.FirstLectureStartDateTime.Value,
                            model.LectureDurationMinutes);
                    }

                    await transaction.CommitAsync();
                });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                PopulateDropdowns(model);
                return View(model);
            }

            TempData["SuccessMessage"] = "✅ تم إضافة الدفعة بنجاح!";
            return RedirectToAction(nameof(Index));
        }


        [AdminPermission("Batches", "Edit")]
        public IActionResult Edit(int id)
        {
            var batch = _context.Batches
                .Include(b => b.Course)
                .Include(b => b.Branch)
                .FirstOrDefault(b => b.Id == id);

            if (batch == null)
                return NotFound();

            var model = new BatchViewModel
            {
                Id = batch.Id,
                Name = batch.Name,
                StartDate = batch.StartDate,
                EndDate = batch.EndDate,
                CourseId = batch.CourseId,
                BranchId = batch.BranchId, // ✅ مباشرة
                Gender = batch.Gender
            };

            PopulateDropdowns(model); // ✅ ضروري

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Batches", "Edit")]
        public IActionResult Edit(BatchViewModel model)
        {
            if (!ModelState.IsValid)
            {
                PopulateDropdowns(model); // ✅ ضروري عند وجود أخطاء
                return View(model);
            }

            var batch = _context.Batches.FirstOrDefault(b => b.Id == model.Id);
            if (batch == null)
                return NotFound();

            batch.Name = model.Name;
            batch.StartDate = model.StartDate;
            batch.EndDate = model.EndDate;
            batch.CourseId = model.CourseId;
            batch.BranchId = model.BranchId;
            batch.Gender = model.Gender;

            _context.Batches.Update(batch);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "✅ تم تعديل الدفعة بنجاح!";
            return RedirectToAction(nameof(Index));
        }


        [AdminPermission("Batches", "Delete")]
        public IActionResult Delete(int id)
        {
            var batch = _context.Batches.Find(id);
            if (batch == null) return NotFound();

            _context.Batches.Remove(batch);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        [AdminPermission("Batches", "Details")]
        public async Task<IActionResult> Details(int id)
        {
            var today = DateTime.Today;
            var now = DateTime.Now;
            var weekStart = today.AddDays(-6);
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var nextMonthStart = monthStart.AddMonths(1);

            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .Include(b => b.Branch)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batch == null)
                return NotFound();

            DateTime batchStartDate = batch.StartDate.Date;
            DateTime batchEndDate = batch.EndDate.HasValue ? batch.EndDate.Value.Date : today;

            var students = await (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join s in _context.Students.AsNoTracking()
                    on e.StudentID equals s.StudentID
                where e.BatchId == id
                select new
                {
                    StudentId = s.StudentID,
                    FullName = s.FullName,
                    StudentEnrollmentStatus = s.EnrollmentStatus,
                    BatchEnrollmentStatus = e.Status
                })
                .ToListAsync();

            var lectures = await (
                from l in _context.Lecture.AsNoTracking()
                join i in _context.Instructors.AsNoTracking()
                    on l.InstructorId equals i.Id
                where l.BatchId == id
                select new BatchLectureProjection
                {
                    LectureId = l.Id,
                    Title = l.Title,
                    Date = l.Date,
                    InstructorId = l.InstructorId,
                    InstructorName = i.FullName,
                    Specialization = i.Specialization,
                    SectionId = l.SectionId
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var attendanceRecords = await (
                from a in _context.AttendanceRecords.AsNoTracking()
                join l in _context.Lecture.AsNoTracking()
                    on a.LectureId equals l.Id
                where l.BatchId == id
                select new BatchAttendanceProjection
                {
                    StudentId = a.StudentId,
                    LectureId = a.LectureId,
                    IsPresent = a.IsPresent,
                    RecordedAt = a.RecordedAt
                })
                .ToListAsync();

            var lessons = await (
                from lesson in _context.Lessons.AsNoTracking()
                join lecture in _context.Lecture.AsNoTracking()
                    on lesson.LectureId equals lecture.Id
                where lecture.BatchId == id
                select new
                {
                    LessonId = lesson.Id,
                    Title = lesson.Title,
                    LectureId = lesson.LectureId,
                    LectureDate = lecture.Date
                })
                .OrderBy(x => x.LectureDate)
                .ToListAsync();

            var homeworkRows = await (
                from hs in _context.HomeworkSets.AsNoTracking()
                join hss in _context.HomeworkSetStudents.AsNoTracking()
                    on hs.Id equals hss.HomeworkSetId
                where hs.BatchId == id
                select new
                {
                    HomeworkSetId = hs.Id,
                    Title = hs.Title,
                    CreatedAt = hs.CreatedAt,
                    EndAt = hs.EndAt,
                    IsSent = hs.IsSent,
                    StudentId = hss.StudentId,
                    IsSubmitted = hss.IsSubmitted,
                    Score = hss.Score
                })
                .ToListAsync();

            var homeworkSets = homeworkRows
                .GroupBy(x => new
                {
                    x.HomeworkSetId,
                    x.Title,
                    x.CreatedAt,
                    x.EndAt,
                    x.IsSent
                })
                .Select(g =>
                {
                    var scoredRows = g.Where(x => x.Score.HasValue).ToList();

                    return new BatchHomeworkSetProjection
                    {
                        HomeworkSetId = g.Key.HomeworkSetId,
                        Title = g.Key.Title,
                        CreatedAt = g.Key.CreatedAt,
                        EndAt = g.Key.EndAt,
                        IsSent = g.Key.IsSent,
                        ExpectedCount = g.Count(),
                        SubmittedCount = g.Count(x => x.IsSubmitted),
                        AverageScore = scoredRows.Count > 0
                            ? Math.Round(scoredRows.Average(x => x.Score!.Value), 2)
                            : 0
                    };
                })
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            var examRows = await (
                from status in _context.ExamStudentStatuses.AsNoTracking()
                join assignment in _context.ExamAssignmentsToBatches.AsNoTracking()
                    on status.ExamAssignmentId equals (int?)assignment.Id
                where assignment.BatchId == id
                select new BatchExamRowProjection
                {
                    AssignmentId = assignment.Id,
                    CreatedAt = assignment.CreatedAt,
                    Title = assignment.Title,
                    StudentId = status.StudentId,
                    IsSubmitted = status.IsSubmitted,
                    Score = status.Score
                })
                .ToListAsync();

            var instructorExamRows = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Where(x => x.BatchId == id)
                .Select(x => new
                {
                    Id = x.Id,
                    CreatedByInstructorId = x.CreatedByInstructorId,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            int studentsCount = students.Count;

            int attendancePresentCount = attendanceRecords.Count(x => x.IsPresent);
            int attendanceTotalCount = attendanceRecords.Count;
            double attendancePercent = CalculatePercent(attendancePresentCount, attendanceTotalCount);

            int homeworkExpectedCount = homeworkRows.Count;
            int homeworkSubmittedCount = homeworkRows.Count(x => x.IsSubmitted);
            double homeworkSubmissionPercent = CalculatePercent(homeworkSubmittedCount, homeworkExpectedCount);

            var scoredExamRows = examRows.Where(x => x.Score.HasValue).ToList();

            double examAverageScore = scoredExamRows.Count > 0
                ? Math.Round(scoredExamRows.Average(x => x.Score!.Value), 2)
                : 0;

            int completedLessonsCount = lessons.Count;
            int totalLessonsCount = completedLessonsCount;

            int daysRemaining = 0;

            if (batchEndDate > today)
                daysRemaining = (batchEndDate - today).Days;

            double courseProgressPercent = CalculateCourseProgress(batchStartDate, batchEndDate, today);

            double healthScore = Math.Round(
                (attendancePercent * 0.30) +
                (homeworkSubmissionPercent * 0.30) +
                (examAverageScore * 0.40),
                2);

            var model = new BatchDetailsDashboardViewModel
            {
                Id = batch.Id,
                Name = batch.Name,
                CourseName = batch.Course != null ? batch.Course.Name : "غير محدد",
                BranchName = batch.Branch != null ? batch.Branch.Name : "غير محدد",
                Gender = batch.Gender.ToString(),
                StartDate = batchStartDate,
                EndDate = batchEndDate,
                AvatarText = BuildAvatar(batch.Name),
                StatusText = ResolveBatchStatus(batchStartDate, batchEndDate, today),
                StageText = BuildStageText(courseProgressPercent),
                Description = "دفعة مرتبطة بدورة " + (batch.Course != null ? batch.Course.Name : "غير محدد") + " داخل فرع " + (batch.Branch != null ? batch.Branch.Name : "غير محدد") + ". تعرض هذه الصفحة حالة الدفعة من حيث الحضور، الواجبات، الاختبارات، الدروس، والمدربين.",
                StudentsCount = studentsCount,
                InstructorsCount = lectures.Select(x => x.InstructorId).Distinct().Count(),
                AttendancePercent = attendancePercent,
                HomeworkSubmissionPercent = homeworkSubmissionPercent,
                ExamAverageScore = examAverageScore,
                CompletedLessonsCount = completedLessonsCount,
                TotalLessonsCount = totalLessonsCount,
                DaysRemaining = daysRemaining,
                CourseProgressPercent = courseProgressPercent,
                HealthScore = healthScore,
                HealthLabel = ResolveHealthLabel(healthScore),
                HealthCssClass = ResolveHealthCss(healthScore),
                AttendanceDeltaText = BuildSimpleDeltaText(attendancePercent, 85, "عن الهدف"),
                HomeworkDeltaText = BuildSimpleDeltaText(homeworkSubmissionPercent, 90, "عن الهدف"),
                ExamDeltaText = BuildSimpleDeltaText(examAverageScore, 80, "عن الهدف")
            };

            model.Lessons = lessons
                .Select((lesson, index) =>
                {
                    var lectureAttendance = attendanceRecords
                        .Where(a => lesson.LectureId.HasValue && a.LectureId == lesson.LectureId.Value)
                        .ToList();

                    double lessonAttendancePercent = CalculatePercent(
                        lectureAttendance.Count(a => a.IsPresent),
                        lectureAttendance.Count);

                    string cssClass = "done";
                    string statusText = "مكتمل";

                    if (lesson.LectureDate.Date == today)
                    {
                        cssClass = "running";
                        statusText = "جارٍ";
                    }
                    else if (lessonAttendancePercent > 0 && lessonAttendancePercent < 70)
                    {
                        cssClass = "low";
                        statusText = "تحذير";
                    }

                    bool hasHomework = homeworkRows.Any(h => h.CreatedAt.Date >= lesson.LectureDate.Date);
                    bool hasExam = examRows.Any(e => e.CreatedAt.Date >= lesson.LectureDate.Date);

                    return new BatchLessonProgressVm
                    {
                        LessonId = lesson.LessonId,
                        Number = index + 1,
                        Title = lesson.Title,
                        AttendancePercent = lessonAttendancePercent,
                        CssClass = cssClass,
                        StatusText = statusText,
                        HasHomework = hasHomework,
                        HasExam = hasExam
                    };
                })
                .Take(28)
                .ToList();

            model.Homeworks = homeworkSets
                .Select(hw =>
                {
                    int missingCount = hw.ExpectedCount - hw.SubmittedCount;
                    double submissionPercent = CalculatePercent(hw.SubmittedCount, hw.ExpectedCount);

                    string statusText = "مغلق";
                    string statusCss = "closed";

                    if (hw.EndAt.HasValue && hw.EndAt.Value >= now)
                    {
                        statusText = "مفتوح";
                        statusCss = "open";

                        if (hw.EndAt.Value <= now.AddDays(2))
                        {
                            statusText = "يغلق قريباً";
                            statusCss = "soon";
                        }
                    }
                    else if (missingCount > 0)
                    {
                        statusText = "متأخر";
                        statusCss = "late";
                    }

                    return new BatchHomeworkDetailsVm
                    {
                        HomeworkSetId = hw.HomeworkSetId,
                        Title = hw.Title,
                        CreatedAt = hw.CreatedAt,
                        EndAt = hw.EndAt,
                        StatusText = statusText,
                        StatusCssClass = statusCss,
                        ExpectedCount = hw.ExpectedCount,
                        SubmittedCount = hw.SubmittedCount,
                        MissingCount = missingCount,
                        SubmissionPercent = submissionPercent,
                        AverageScore = hw.AverageScore,
                        MissingCssClass = missingCount == 0 ? "none" : missingCount <= 3 ? "few" : "many"
                    };
                })
                .Take(10)
                .ToList();

            model.Instructors = lectures
                .GroupBy(x => new
                {
                    x.InstructorId,
                    x.InstructorName,
                    x.Specialization
                })
                .Select(g =>
                {
                    int instructorLecturesThisWeek = g.Count(x => x.Date >= weekStart && x.Date < today.AddDays(1));

                    int instructorExams = instructorExamRows
                        .Count(x => x.CreatedByInstructorId.HasValue && x.CreatedByInstructorId.Value == g.Key.InstructorId);

                    int instructorPossibleAttendance = 0;
                    int instructorPresentAttendance = 0;

                    foreach (var lecture in g)
                    {
                        var lectureAttendance = attendanceRecords
                            .Where(x => x.LectureId == lecture.LectureId)
                            .ToList();

                        instructorPossibleAttendance += lectureAttendance.Count;
                        instructorPresentAttendance += lectureAttendance.Count(x => x.IsPresent);
                    }

                    double participation = CalculatePercent(instructorPresentAttendance, instructorPossibleAttendance);

                    double activityScore = Math.Round(
                        Math.Min(100, (instructorLecturesThisWeek * 12) + (instructorExams * 18)) * 0.45 +
                        participation * 0.55,
                        2);

                    return new BatchInstructorDetailsVm
                    {
                        InstructorId = g.Key.InstructorId,
                        FullName = g.Key.InstructorName,
                        Specialization = string.IsNullOrWhiteSpace(g.Key.Specialization) ? "مدرب" : g.Key.Specialization,
                        AvatarText = BuildAvatar(g.Key.InstructorName),
                        LecturesThisWeek = instructorLecturesThisWeek,
                        HomeworksCount = model.Homeworks.Count,
                        ExamsCount = instructorExams,
                        StudentParticipationPercent = participation,
                        ActivityScore = activityScore,
                        ActivityCssColor = activityScore >= 80 ? "var(--green)" : activityScore >= 60 ? "var(--blue)" : "var(--amber)"
                    };
                })
                .OrderByDescending(x => x.ActivityScore)
                .ToList();

            var studentPerformanceRows = students.Select(student =>
            {
                int studentAttendanceTotal = attendanceRecords.Count(x => x.StudentId == student.StudentId);
                int studentAttendancePresent = attendanceRecords.Count(x => x.StudentId == student.StudentId && x.IsPresent);
                double studentAttendancePercent = CalculatePercent(studentAttendancePresent, studentAttendanceTotal);

                int studentHomeworkTotal = homeworkRows.Count(x => x.StudentId == student.StudentId);
                int studentHomeworkSubmitted = homeworkRows.Count(x => x.StudentId == student.StudentId && x.IsSubmitted);
                double studentHomeworkPercent = CalculatePercent(studentHomeworkSubmitted, studentHomeworkTotal);
                int missingHomework = studentHomeworkTotal - studentHomeworkSubmitted;

                var studentExamRows = examRows
                    .Where(x => x.StudentId == student.StudentId && x.Score.HasValue)
                    .ToList();

                double studentExamAverage = studentExamRows.Count > 0
                    ? Math.Round(studentExamRows.Average(x => x.Score!.Value), 2)
                    : 0;

                double finalScore = Math.Round(
                    (studentAttendancePercent * 0.30) +
                    (studentHomeworkPercent * 0.30) +
                    (studentExamAverage * 0.40),
                    2);

                var flags = new List<string>();

                if (studentAttendancePercent < 70)
                    flags.Add("غياب " + (100 - studentAttendancePercent).ToString("0.##") + "%");

                if (missingHomework > 0)
                    flags.Add(missingHomework + " واجبات متراكمة");

                if (studentExamAverage > 0 && studentExamAverage < 60)
                    flags.Add("متوسط " + studentExamAverage.ToString("0.##") + "%");

                return new BatchStudentPerformanceVm
                {
                    StudentId = student.StudentId,
                    FullName = student.FullName,
                    AvatarText = BuildAvatar(student.FullName),
                    AttendancePercent = studentAttendancePercent,
                    HomeworkSubmissionPercent = studentHomeworkPercent,
                    AverageScore = finalScore,
                    MissingHomeworksCount = missingHomework,
                    TrendCssClass = finalScore >= 80 ? "up" : finalScore < 60 ? "dn" : "eq",
                    TrendText = finalScore >= 80 ? "↑ جيد" : finalScore < 60 ? "↓ خطر" : "→ ثابت",
                    Flags = flags
                };
            }).ToList();

            model.TopStudents = studentPerformanceRows
                .OrderByDescending(x => x.AverageScore)
                .Take(5)
                .Select((x, index) =>
                {
                    x.Rank = index + 1;
                    return x;
                })
                .ToList();

            model.RiskStudents = studentPerformanceRows
                .OrderBy(x => x.AverageScore)
                .Take(5)
                .Select((x, index) =>
                {
                    x.Rank = index + 1;
                    return x;
                })
                .ToList();

            model.ProgressBars = new List<BatchProgressBarVm>
    {
        new BatchProgressBarVm
        {
            Label = "نسبة الحضور",
            Value = attendancePercent,
            Target = 85,
            CssColor = attendancePercent >= 85 ? "var(--green)" : "var(--amber)"
        },
        new BatchProgressBarVm
        {
            Label = "تسليم الواجبات",
            Value = homeworkSubmissionPercent,
            Target = 90,
            CssColor = homeworkSubmissionPercent >= 90 ? "var(--green)" : "var(--amber)"
        },
        new BatchProgressBarVm
        {
            Label = "متوسط الاختبارات",
            Value = examAverageScore,
            Target = 80,
            CssColor = examAverageScore >= 80 ? "var(--green)" : "var(--red)"
        },
        new BatchProgressBarVm
        {
            Label = "تغطية المنهج",
            Value = courseProgressPercent,
            Target = 65,
            CssColor = "var(--blue)"
        },
        new BatchProgressBarVm
        {
            Label = "صحة الدفعة",
            Value = healthScore,
            Target = 80,
            CssColor = healthScore >= 80 ? "var(--green)" : healthScore >= 60 ? "var(--blue)" : "var(--red)"
        }
    };

            model.DiagnosisItems = BuildDiagnosis(model);
            model.AttendanceCalendar = BuildAttendanceCalendar(monthStart, nextMonthStart, attendanceRecords, today);
            model.HeatmapWeeks = BuildHeatmap(today, lectures, homeworkSets, examRows);

            return View(model);
        }


        private static double CalculatePercent(int value, int total)
        {
            if (total <= 0)
                return 0;

            return Math.Round((value / (double)total) * 100, 2);
        }

        private static double CalculateCourseProgress(DateTime startDate, DateTime endDate, DateTime today)
        {
            if (endDate.Date <= startDate.Date)
                return 0;

            if (today.Date <= startDate.Date)
                return 0;

            if (today.Date >= endDate.Date)
                return 100;

            double totalDays = (endDate.Date - startDate.Date).TotalDays;
            double elapsedDays = (today.Date - startDate.Date).TotalDays;

            return Math.Round((elapsedDays / totalDays) * 100, 2);
        }

        private static string ResolveBatchStatus(DateTime startDate, DateTime endDate, DateTime today)
        {
            if (today.Date < startDate.Date)
                return "لم تبدأ";

            if (today.Date > endDate.Date)
                return "منتهية";

            return "نشطة";
        }

        private static string BuildStageText(double progress)
        {
            if (progress < 25)
                return "المرحلة 1 من 4";

            if (progress < 50)
                return "المرحلة 2 من 4";

            if (progress < 75)
                return "المرحلة 3 من 4";

            return "المرحلة 4 من 4";
        }

        private static string ResolveHealthLabel(double score)
        {
            if (score >= 85)
                return "ممتاز";

            if (score >= 70)
                return "جيد";

            if (score >= 55)
                return "يحتاج متابعة";

            return "خطر";
        }

        private static string ResolveHealthCss(double score)
        {
            if (score >= 85)
                return "ex";

            if (score >= 70)
                return "gd";

            if (score >= 55)
                return "wn";

            return "dg";
        }

        private static string BuildSimpleDeltaText(double value, double target, string suffix)
        {
            double diff = Math.Round(value - target, 2);

            if (diff > 0)
                return "↑ +" + diff.ToString("0.##") + "% " + suffix;

            if (diff < 0)
                return "↓ " + diff.ToString("0.##") + "% " + suffix;

            return "مطابق للهدف";
        }

        private static string BuildAvatar(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "دف";

            string cleaned = text.Trim();

            if (cleaned.Length == 1)
                return cleaned;

            var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
                return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpper();

            return cleaned.Substring(0, Math.Min(2, cleaned.Length)).ToUpper();
        }

        private static List<BatchDiagnosisItemVm> BuildDiagnosis(BatchDetailsDashboardViewModel model)
        {
            var result = new List<BatchDiagnosisItemVm>();

            if (model.ExamAverageScore >= 80)
            {
                result.Add(new BatchDiagnosisItemVm
                {
                    CssClass = "str",
                    Icon = "💪",
                    Title = "نقطة قوة — درجات الاختبارات",
                    Body = "متوسط الاختبارات " + model.ExamAverageScore.ToString("0.##") + "%، وهذا يشير إلى قدرة جيدة على استيعاب المحتوى."
                });
            }

            if (model.HomeworkSubmissionPercent < 75)
            {
                result.Add(new BatchDiagnosisItemVm
                {
                    CssClass = "wkn",
                    Icon = "⚠️",
                    Title = "نقطة ضعف — تسليم الواجبات",
                    Body = "نسبة تسليم الواجبات " + model.HomeworkSubmissionPercent.ToString("0.##") + "%، وتحتاج متابعة مباشرة مع الطلاب المتأخرين."
                });
            }

            if (model.AttendancePercent < 75)
            {
                result.Add(new BatchDiagnosisItemVm
                {
                    CssClass = "risk",
                    Icon = "🚨",
                    Title = "خطر — انخفاض الحضور",
                    Body = "نسبة الحضور " + model.AttendancePercent.ToString("0.##") + "%، وقد تؤثر على تقدم الدفعة في الدروس القادمة."
                });
            }

            if (model.RiskStudents.Count > 0)
            {
                result.Add(new BatchDiagnosisItemVm
                {
                    CssClass = "opp",
                    Icon = "🎯",
                    Title = "فرصة تدخل — طلاب يحتاجون متابعة",
                    Body = "يوجد " + model.RiskStudents.Count + " طلاب في أسفل قائمة الأداء. الأفضل إرسال تنبيه ومتابعة فردية."
                });
            }

            if (result.Count == 0)
            {
                result.Add(new BatchDiagnosisItemVm
                {
                    CssClass = "str",
                    Icon = "✅",
                    Title = "حالة مستقرة",
                    Body = "لا توجد مؤشرات حرجة حالياً. الدفعة تعمل ضمن نطاق مستقر."
                });
            }

            return result;
        }

        private static List<BatchAttendanceDayVm> BuildAttendanceCalendar(
            DateTime monthStart,
            DateTime nextMonthStart,
            List<BatchAttendanceProjection> attendanceRecords,
            DateTime today)
        {
            var result = new List<BatchAttendanceDayVm>();

            DateTime cursor = monthStart;

            while (cursor < nextMonthStart)
            {
                var dayRecords = attendanceRecords
                    .Where(x => x.RecordedAt.Date == cursor.Date)
                    .ToList();

                double percent = CalculatePercent(
                    dayRecords.Count(x => x.IsPresent),
                    dayRecords.Count);

                string cssClass = "no-lecture";

                if (dayRecords.Count > 0)
                {
                    if (percent >= 80)
                        cssClass = "high";
                    else if (percent >= 60)
                        cssClass = "mid";
                    else
                        cssClass = "low";
                }

                if (cursor.Date == today.Date)
                    cssClass += " today";

                result.Add(new BatchAttendanceDayVm
                {
                    Date = cursor,
                    DayText = cursor.Day.ToString(),
                    Percent = percent,
                    CssClass = cssClass,
                    HasLecture = dayRecords.Count > 0,
                    IsToday = cursor.Date == today.Date
                });

                cursor = cursor.AddDays(1);
            }

            return result;
        }

        private static List<BatchHeatmapWeekVm> BuildHeatmap(
            DateTime today,
            List<BatchLectureProjection> lectures,
            List<BatchHomeworkSetProjection> homeworks,
            List<BatchExamRowProjection> examRows)
        {
            var result = new List<BatchHeatmapWeekVm>();

            DateTime start = today.Date.AddDays(-20);

            for (int week = 0; week < 3; week++)
            {
                var weekVm = new BatchHeatmapWeekVm();

                for (int day = 0; day < 7; day++)
                {
                    DateTime current = start.AddDays((week * 7) + day);

                    int lecturesCount = lectures.Count(x => x.Date.Date == current.Date);
                    int homeworkCount = homeworks.Count(x => x.CreatedAt.Date == current.Date);
                    int examCount = examRows.Count(x => x.CreatedAt.Date == current.Date);

                    int total = lecturesCount + homeworkCount + examCount;

                    string style = "background:var(--bg-2);color:var(--mute)";

                    if (total >= 8)
                        style = "background:#1d4ed8;color:#fff";
                    else if (total >= 5)
                        style = "background:#3b82f6;color:#fff";
                    else if (total >= 3)
                        style = "background:#93c5fd;color:#1e3a8a";
                    else if (total > 0)
                        style = "background:#dbeafe;color:#1e3a8a";

                    weekVm.Days.Add(new BatchHeatmapDayVm
                    {
                        ActivityCount = total,
                        CssStyle = style
                    });
                }

                result.Add(weekVm);
            }

            return result;
        }






        [HttpGet]
        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee")]
        public async Task<IActionResult> BatchStudents(int batchId)
        {
            var batch = await _context.Batches
                .Include(b => b.Course)
                .Include(b => b.Branch)   // ✅ إضافة الفرع
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                return NotFound("❌ لم يتم العثور على الدفعة.");

            // ✅ جلب بيانات الطلاب بشكل Strongly Typed
            var students = await (
                from e in _context.StudentBatchEnrollments
                join s in _context.Students on e.StudentID equals s.StudentID
                where e.BatchId == batchId
                select new QdratNew.ViewModels.Batch.BatchStudentVm
                {
                    StudentId = s.StudentID,
                    FullName = s.FullName,
                    PhoneNumber = s.PhoneNumber ?? "",
                    NationalId = s.NationalID,     // ← الاسم الصحيح من الكيان Student
                    Stage = s.Level ?? "",          // ← نستخدم Level كمرحلة
                    Level = s.Level ?? "",
                    Gender = s.Gender ?? "",
                    EnrolledAt = e.EnrolledAt,
                    Status = e.Status ?? ""
                }
            ).ToListAsync();

            // 💡 بناء الـ ViewModel الرئيسي
            var model = new QdratNew.ViewModels.Batch.BatchStudentsViewModel
            {
                BatchId = batch.Id,
                BatchName = batch.Name,

                BranchId = batch.BranchId,                     // ✅
                BranchName = batch.Branch?.Name ?? "غير محدد", // ✅

                CourseName = batch.Course?.Name ?? "غير محدد",
                Students = students
            };

            return View("~/Areas/Admin/Views/Batches/BatchStudents.cshtml", model);
        }


        private void PopulateDropdowns(BatchViewModel model)
        {
            model.Courses = _context.Courses
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                }).ToList();

            model.Branches = _context.Branches
                .AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToList();
        }


        private class BatchAttendanceProjection
        {
            public int StudentId { get; set; }
            public int LectureId { get; set; }
            public bool IsPresent { get; set; }
            public DateTime RecordedAt { get; set; }
        }

        private class BatchLectureProjection
        {
            public int LectureId { get; set; }
            public string Title { get; set; } = "";
            public DateTime Date { get; set; }
            public int InstructorId { get; set; }
            public string InstructorName { get; set; } = "";
            public string? Specialization { get; set; }
            public int? SectionId { get; set; }
        }

        private class BatchHomeworkSetProjection
        {
            public int HomeworkSetId { get; set; }
            public string Title { get; set; } = "";
            public DateTime CreatedAt { get; set; }
            public DateTime? EndAt { get; set; }
            public bool IsSent { get; set; }
            public int ExpectedCount { get; set; }
            public int SubmittedCount { get; set; }
            public double AverageScore { get; set; }
        }

        private class BatchExamRowProjection
        {
            public int AssignmentId { get; set; }
            public DateTime CreatedAt { get; set; }
            public string Title { get; set; } = "";
            public int StudentId { get; set; }
            public bool IsSubmitted { get; set; }
            public int? Score { get; set; }
        }

        // ============================================================
        // عرض تفاصيل واجب منزلي محدد داخل الدفعة
        // ============================================================

        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee")]
        public async Task<IActionResult> HomeworkSetDetail(int homeworkSetId, int batchId)
        {
            var hwSet = await _context.HomeworkSets
                .AsNoTracking()
                .Include(h => h.Batch).ThenInclude(b => b.Course)
                .Include(h => h.AssignedByUser)
                .FirstOrDefaultAsync(h => h.Id == homeworkSetId && h.BatchId == batchId);

            if (hwSet == null)
                return NotFound();

            var studentRows = await (
                from hss in _context.HomeworkSetStudents.AsNoTracking()
                join s in _context.Students.AsNoTracking() on hss.StudentId equals s.StudentID
                where hss.HomeworkSetId == homeworkSetId
                select new
                {
                    s.StudentID,
                    s.FullName,
                    hss.IsSubmitted,
                    hss.SubmittedAt,
                    hss.Score,
                    hss.AssignedAt
                }).ToListAsync();

            var scoredRows = studentRows.Where(r => r.Score.HasValue).ToList();
            double avgScore   = scoredRows.Count > 0 ? Math.Round(scoredRows.Average(r => r.Score!.Value), 1) : 0;
            double maxScore   = scoredRows.Count > 0 ? scoredRows.Max(r => r.Score!.Value) : 0;
            double minScore   = scoredRows.Count > 0 ? scoredRows.Min(r => r.Score!.Value) : 0;

            var studentVms = studentRows.OrderBy(r => !r.IsSubmitted).ThenBy(r => r.FullName)
                .Select(r =>
                {
                    var scoreClass = !r.Score.HasValue ? "neutral"
                        : r.Score.Value >= 75 ? "good"
                        : r.Score.Value >= 55 ? "warn" : "bad";

                    var nameParts = (r.FullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var avatar = nameParts.Length >= 2
                        ? $"{nameParts[0][0]}{nameParts[1][0]}"
                        : (r.FullName?.Length >= 2 ? r.FullName[..2] : "ط");

                    return new HomeworkStudentRowVm
                    {
                        StudentId    = r.StudentID,
                        FullName     = r.FullName ?? "—",
                        AvatarText   = avatar,
                        IsSubmitted  = r.IsSubmitted,
                        SubmittedAt  = r.SubmittedAt,
                        Score        = r.Score,
                        ScoreCssClass= scoreClass,
                        IsLate       = r.IsSubmitted && hwSet.EndAt.HasValue && r.SubmittedAt.HasValue
                                        && r.SubmittedAt.Value > hwSet.EndAt.Value
                    };
                }).ToList();

            var assignedByName = hwSet.AssignedByUser?.UserName
                ?? hwSet.AssignedByUser?.Email
                ?? "غير محدد";

            var vm = new HomeworkSetDetailViewModel
            {
                HomeworkSetId  = hwSet.Id,
                Title          = hwSet.Title,
                BatchId        = batchId,
                BatchName      = hwSet.Batch?.Name ?? $"دفعة #{batchId}",
                CourseName     = hwSet.Batch?.Course?.Name ?? "—",
                InstructorName = assignedByName,
                CreatedAt      = hwSet.CreatedAt,
                StartAt        = hwSet.StartAt,
                EndAt          = hwSet.EndAt,
                IsSent         = hwSet.IsSent,
                IsClosed       = hwSet.IsClosed,
                AllowRetake    = hwSet.AllowRetake,
                MaxRetakes     = hwSet.MaxRetakes,
                ExpectedCount  = studentRows.Count,
                SubmittedCount = studentRows.Count(r => r.IsSubmitted),
                AverageScore   = avgScore,
                MaxScore       = maxScore,
                MinScore       = minScore,
                Students       = studentVms
            };

            return View(vm);
        }

        // ============================================================
        // عرض أداء مدرب محدد داخل الدفعة
        // ============================================================

        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer")]
        public async Task<IActionResult> InstructorBatchPerformance(int instructorId, int batchId)
        {
            var instructor = await _context.Instructors
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == instructorId);

            if (instructor == null) return NotFound();

            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null) return NotFound();

            var today     = DateTime.Today;
            var weekStart = today.AddDays(-6);

            // طلاب الدفعة
            var studentIds = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == batchId)
                .Select(e => e.StudentID)
                .ToListAsync();
            int totalStudents = studentIds.Count;

            // محاضرات المدرب في الدفعة
            var lectures = await _context.Lecture
                .AsNoTracking()
                .Where(l => l.BatchId == batchId && l.InstructorId == instructorId)
                .OrderByDescending(l => l.Date)
                .Select(l => new { l.Id, l.Title, l.Date })
                .ToListAsync();

            var lectureIds = lectures.Select(l => l.Id).ToHashSet();

            // سجلات الحضور
            var attendance = await _context.AttendanceRecords
                .AsNoTracking()
                .Where(a => lectureIds.Contains(a.LectureId))
                .Select(a => new { a.LectureId, a.StudentId, a.IsPresent })
                .ToListAsync();

            var attendanceByLecture = attendance
                .GroupBy(a => a.LectureId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var lectureVms = lectures.Select(l =>
            {
                var recs = attendanceByLecture.TryGetValue(l.Id, out var r) ? r : new();
                int present  = recs.Count(x => x.IsPresent);
                int expected = totalStudents > 0 ? totalStudents : recs.Count;
                return new InstructorLectureRowVm
                {
                    LectureId    = l.Id,
                    Title        = l.Title,
                    Date         = l.Date,
                    PresentCount = present,
                    TotalStudents= expected
                };
            }).ToList();

            // واجبات المدرب في الدفعة (مرتبطة بمحاضراته)
            var hwRows = await (
                from hs in _context.HomeworkSets.AsNoTracking()
                join hss in _context.HomeworkSetStudents.AsNoTracking()
                    on hs.Id equals hss.HomeworkSetId
                where hs.BatchId == batchId && lectureIds.Contains(hs.LectureId ?? -1)
                select new { hs.Id, hs.Title, hs.CreatedAt, hs.EndAt, hss.IsSubmitted, hss.Score }
            ).ToListAsync();

            var hwVms = hwRows
                .GroupBy(r => new { r.Id, r.Title, r.CreatedAt, r.EndAt })
                .Select(g =>
                {
                    var scored = g.Where(x => x.Score.HasValue).ToList();
                    return new InstructorHomeworkRowVm
                    {
                        HomeworkSetId   = g.Key.Id,
                        Title           = g.Key.Title,
                        CreatedAt       = g.Key.CreatedAt,
                        EndAt           = g.Key.EndAt,
                        SubmittedCount  = g.Count(x => x.IsSubmitted),
                        ExpectedCount   = g.Select(x => x.IsSubmitted).Count(),
                        AverageScore    = scored.Count > 0
                            ? Math.Round(scored.Average(x => x.Score!.Value), 1) : 0
                    };
                }).OrderByDescending(x => x.CreatedAt).ToList();

            // اختبارات أنشأها المدرب في الدفعة
            var examRows = await (
                from a in _context.ExamAssignmentsToBatches.AsNoTracking()
                join s in _context.ExamStudentStatuses.AsNoTracking()
                    on (int?)a.Id equals s.ExamAssignmentId
                where a.BatchId == batchId && a.CreatedByInstructorId == instructorId
                select new { a.Id, a.Title, a.CreatedAt, s.IsSubmitted, s.Score }
            ).ToListAsync();

            var examVms = examRows
                .GroupBy(r => new { r.Id, r.Title, r.CreatedAt })
                .Select(g =>
                {
                    var scored = g.Where(x => x.Score.HasValue).ToList();
                    return new InstructorExamRowVm
                    {
                        AssignmentId   = g.Key.Id,
                        Title          = g.Key.Title,
                        CreatedAt      = g.Key.CreatedAt,
                        SubmittedCount = g.Count(x => x.IsSubmitted),
                        TotalStudents  = totalStudents,
                        AverageScore   = scored.Count > 0
                            ? Math.Round(scored.Average(x => x.Score!.Value), 1) : 0
                    };
                }).OrderByDescending(x => x.CreatedAt).ToList();

            // حساب نسب الأداء
            double avgAttendance = lectureVms.Count > 0
                ? Math.Round(lectureVms.Average(l => l.AttendancePercent), 1) : 0;
            double avgHwSubmission = hwVms.Count > 0
                ? Math.Round(hwVms.Average(h => h.SubmissionPercent), 1) : 0;
            double avgExamScore = examVms.Count > 0
                ? Math.Round(examVms.Average(e => e.AverageScore), 1) : 0;

            double activity = (avgAttendance * 0.5) + (avgHwSubmission * 0.3) + (avgExamScore * 0.2);
            activity = Math.Min(100, Math.Round(activity, 1));
            string activityColor = activity >= 80 ? "#059669" : activity >= 60 ? "#2563eb" : activity >= 40 ? "#d97706" : "#dc2626";

            // مشاكل مكتشفة
            var issues = new List<InstructorBatchIssueVm>();

            var lowAttLectures = lectureVms.Where(l => l.IsLowAttendance).ToList();
            if (lowAttLectures.Count > 0)
                issues.Add(new() {
                    Icon     = "📉",
                    CssClass = "bad",
                    Title    = $"{lowAttLectures.Count} محاضرة بنسبة حضور أقل من 60%",
                    Body     = string.Join("، ", lowAttLectures.Take(3).Select(l => l.Title))
                });

            var lowHwVms = hwVms.Where(h => h.SubmissionPercent < 60).ToList();
            if (lowHwVms.Count > 0)
                issues.Add(new() {
                    Icon     = "📝",
                    CssClass = "warn",
                    Title    = $"{lowHwVms.Count} واجب بنسبة تسليم منخفضة",
                    Body     = string.Join("، ", lowHwVms.Take(3).Select(h => h.Title))
                });

            if (lectureVms.Count == 0)
                issues.Add(new() {
                    Icon     = "🚫",
                    CssClass = "bad",
                    Title    = "لا توجد محاضرات مسجلة لهذا المدرب في الدفعة",
                    Body     = "تأكد من ربط المحاضرات بالمدرب بشكل صحيح."
                });

            if (issues.Count == 0)
                issues.Add(new() {
                    Icon     = "✅",
                    CssClass = "good",
                    Title    = "لا توجد مشاكل مكتشفة",
                    Body     = "أداء المدرب في هذه الدفعة ضمن المعدلات المقبولة."
                });

            var nameParts = (instructor.FullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var avatar = nameParts.Length >= 2
                ? $"{nameParts[0][0]}{nameParts[1][0]}"
                : (instructor.FullName?.Length >= 2 ? instructor.FullName[..2] : "مد");

            var vm = new InstructorBatchPerformanceViewModel
            {
                InstructorId                  = instructor.Id,
                FullName                      = instructor.FullName,
                Specialization                = instructor.Specialization ?? "مدرب",
                AvatarText                    = avatar,
                IsActive                      = instructor.IsActive,
                BatchId                       = batchId,
                BatchName                     = batch.Name,
                CourseName                    = batch.Course?.Name ?? "—",
                TotalStudents                 = totalStudents,
                TotalLectures                 = lectures.Count,
                LecturesThisWeek              = lectures.Count(l => l.Date >= weekStart),
                TotalHomeworksCreated         = hwVms.Count,
                TotalExamsCreated             = examVms.Count,
                OverallAttendancePercent      = avgAttendance,
                OverallHomeworkSubmissionPercent = avgHwSubmission,
                OverallExamAvgScore           = avgExamScore,
                ActivityScore                 = activity,
                ActivityCssColor              = activityColor,
                Lectures                      = lectureVms,
                Homeworks                     = hwVms,
                Exams                         = examVms,
                Issues                        = issues
            };

            return View(vm);
        }

        // ============================================================
        // إعادة تعيين واجب طالب محدد (تصفير التسليم والدرجة والمحاولات)
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer")]
        public async Task<IActionResult> ResetStudentHomework(int homeworkSetId, int studentId, int batchId)
        {
            var hwStudent = await _context.HomeworkSetStudents
                .FirstOrDefaultAsync(h => h.HomeworkSetId == homeworkSetId && h.StudentId == studentId);

            if (hwStudent == null)
            {
                TempData["ErrorMessage"] = "لم يتم العثور على سجل الطالب في هذا الواجب.";
                return RedirectToAction(nameof(HomeworkSetDetail), new { homeworkSetId, batchId });
            }

            // تصفير حالة التسليم والدرجة
            hwStudent.IsSubmitted   = false;
            hwStudent.SubmittedAt   = null;
            hwStudent.Score         = null;
            hwStudent.LastUpdated   = DateTime.Now;

            // حذف محاولات الأسئلة المرتبطة بهذا الواجب والطالب
            var attempts = await _context.QuestionAttemptNew
                .Where(q => q.HomeworkSetId == homeworkSetId && q.StudentId == studentId)
                .ToListAsync();
            _context.QuestionAttemptNew.RemoveRange(attempts);

            // حذف سجلات محاولات الواجب
            var setAttempts = await _context.HomeworkSetAttempts
                .Where(a => a.HomeworkSetId == homeworkSetId && a.StudentId == studentId)
                .ToListAsync();
            _context.HomeworkSetAttempts.RemoveRange(setAttempts);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ تم إعادة تعيين الواجب للطالب. يمكنه الآن حله من جديد.";
            return RedirectToAction(nameof(HomeworkSetDetail), new { homeworkSetId, batchId });
        }

        // ============================================================
        // إرسال إشعار لطالب محدد من صفحة تفاصيل الواجب
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee")]
        public async Task<IActionResult> SendHomeworkStudentNotification(
            int homeworkSetId, int batchId, string studentIds, string message, string notifType)
        {
            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(studentIds))
            {
                TempData["ErrorMessage"] = "نص الإشعار ومعرفات الطلاب مطلوبان.";
                return RedirectToAction(nameof(HomeworkSetDetail), new { homeworkSetId, batchId });
            }

            var ids = studentIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : -1)
                .Where(id => id > 0).ToList();

            var hwSet = await _context.HomeworkSets.AsNoTracking()
                .Select(h => new { h.Id, h.Title })
                .FirstOrDefaultAsync(h => h.Id == homeworkSetId);

            var typeLabel = notifType switch
            {
                "reminder" => "📌 تذكير بالواجب",
                "warning"  => "⚠ تنبيه تأخر",
                "result"   => "🏆 نتيجة الواجب",
                _          => "📢 إشعار واجب"
            };

            var category = notifType == "warning"
                ? QdratNew.Enums.NotificationCategory.Important
                : QdratNew.Enums.NotificationCategory.Homework;

            var now = DateTime.Now;
            var notifications = ids.Select(sid => new Notification
            {
                StudentID  = sid,
                Message    = $"{typeLabel} — {hwSet?.Title ?? "واجب"}: {message}",
                SentAt     = now,
                IsRead     = false,
                Category   = category,
                TargetUrl  = Url.Action("HomeworkSetDetail", "Batches",
                    new { area = "Admin", homeworkSetId, batchId })
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ تم إرسال الإشعار لـ {notifications.Count} طالب.";
            return RedirectToAction(nameof(HomeworkSetDetail), new { homeworkSetId, batchId });
        }

        // ============================================================
        // أكشنات أزرار البطاقة العلوية في صفحة تفاصيل الدفعة
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee")]
        public async Task<IActionResult> SendBatchMessage(int batchId, string subject, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "نص الرسالة مطلوب.";
                return RedirectToAction(nameof(Details), new { id = batchId });
            }

            var studentIds = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == batchId)
                .Select(e => e.StudentID)
                .ToListAsync();

            var batchName = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync() ?? $"دفعة #{batchId}";

            var now = DateTime.Now;
            var notifications = studentIds.Select(sid => new Notification
            {
                StudentID = sid,
                Message = $"✉ [{batchName}] {subject}: {message}",
                SentAt = now,
                IsRead = false,
                Category = QdratNew.Enums.NotificationCategory.General,
                TargetUrl = Url.Action("Details", "Batches", new { area = "Admin", id = batchId })
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ تم إرسال الرسالة إلى {notifications.Count} طالب في الدفعة.";
            return RedirectToAction(nameof(Details), new { id = batchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee")]
        public async Task<IActionResult> SendBatchFollowUpNotification(int batchId, string followUpType, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "نص الإشعار مطلوب.";
                return RedirectToAction(nameof(Details), new { id = batchId });
            }

            var studentIds = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == batchId)
                .Select(e => e.StudentID)
                .ToListAsync();

            var batchName = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync() ?? $"دفعة #{batchId}";

            var typeLabel = followUpType switch
            {
                "attendance" => "⚠ متابعة الحضور",
                "homework" => "⚠ متابعة الواجبات",
                "exam" => "⚠ متابعة الاختبارات",
                _ => "⚠ إشعار متابعة"
            };

            var now = DateTime.Now;
            var notifications = studentIds.Select(sid => new Notification
            {
                StudentID = sid,
                Message = $"{typeLabel} - {batchName}: {message}",
                SentAt = now,
                IsRead = false,
                Category = QdratNew.Enums.NotificationCategory.Reminder,
                TargetUrl = Url.Action("Details", "Batches", new { area = "Admin", id = batchId })
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ تم إرسال إشعار المتابعة إلى {notifications.Count} طالب.";
            return RedirectToAction(nameof(Details), new { id = batchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer")]
        public async Task<IActionResult> SendBatchUrgentAlert(int batchId, string urgentReason, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "نص الإجراء العاجل مطلوب.";
                return RedirectToAction(nameof(Details), new { id = batchId });
            }

            var studentIds = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == batchId)
                .Select(e => e.StudentID)
                .ToListAsync();

            var batchName = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync() ?? $"دفعة #{batchId}";

            var now = DateTime.Now;
            var notifications = studentIds.Select(sid => new Notification
            {
                StudentID = sid,
                Message = $"🚨 إجراء عاجل - {batchName} [{urgentReason}]: {message}",
                SentAt = now,
                IsRead = false,
                Category = QdratNew.Enums.NotificationCategory.Important,
                TargetUrl = Url.Action("Details", "Batches", new { area = "Admin", id = batchId })
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"🚨 تم إرسال الإجراء العاجل إلى {notifications.Count} طالب.";
            return RedirectToAction(nameof(Details), new { id = batchId });
        }

        // ─── إدارة وصول أرشيف الدفعات (Owner/Developer only) ────────
        [HttpGet]
        [AdminPermission("Batches", "Archive")]
        public async Task<IActionResult> ManageArchiveAccess(int batchId)
        {
            if (!IsBatchesArchiveOwner())
                return Forbid();

            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null) return NotFound();

            if (!batch.IsArchived)
            {
                TempData["ErrorMessage"] = "إدارة موافقات الأرشيف متاحة للدفعات المؤرشفة فقط.";
                return RedirectToAction(nameof(Index));
            }

            var users = await GetArchiveAccessCandidatesAsync();
            var activeUserIds = (await _context.BatchesArchiveAccesses
                .AsNoTracking()
                .Where(x => x.BatchId == batchId && x.IsActive)
                .Select(x => x.UserId)
                .ToListAsync()).ToHashSet();

            var items = new List<BatchArchiveAccessUserItem>();
            foreach (var user in users.OrderBy(x => x.FullName ?? x.UserName))
            {
                var roles = await _userManager.GetRolesAsync(user);
                items.Add(new BatchArchiveAccessUserItem
                {
                    UserId      = user.Id,
                    DisplayName = user.FullName ?? user.UserName ?? user.Email ?? user.Id,
                    Email       = user.Email ?? string.Empty,
                    Roles       = string.Join("، ", roles),
                    IsAllowed   = activeUserIds.Contains(user.Id)
                });
            }

            var vm = new BatchArchiveAccessVM
            {
                BatchId     = batch.Id,
                BatchName   = batch.Name,
                CourseTitle = batch.Course?.Name ?? string.Empty,
                Users       = items
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Batches", "Archive")]
        public async Task<IActionResult> UpdateArchiveAccess(int batchId, List<string> allowedUserIds)
        {
            if (!IsBatchesArchiveOwner())
                return Forbid();

            var batch = await _context.Batches.FirstOrDefaultAsync(b => b.Id == batchId);
            if (batch == null) return NotFound();

            if (!batch.IsArchived)
            {
                TempData["ErrorMessage"] = "لا يمكن تعديل موافقات الأرشيف لدفعة غير مؤرشفة.";
                return RedirectToAction(nameof(Index));
            }

            allowedUserIds ??= new List<string>();
            var allowedSet = allowedUserIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct().ToHashSet();

            var candidateUsers   = await GetArchiveAccessCandidatesAsync();
            var candidateUserIds = candidateUsers.Select(x => x.Id).ToHashSet();
            allowedSet.RemoveWhere(x => !candidateUserIds.Contains(x));

            var existing        = await _context.BatchesArchiveAccesses
                .Where(x => x.BatchId == batchId).ToListAsync();
            var existingUserIds = existing.Select(x => x.UserId).ToHashSet();
            var now             = DateTime.UtcNow;
            var currentUserId   = CurrentUserId();

            foreach (var access in existing)
                access.IsActive = allowedSet.Contains(access.UserId);

            foreach (var userId in allowedSet.Where(x => !existingUserIds.Contains(x)))
            {
                _context.BatchesArchiveAccesses.Add(new BatchesArchiveAccess
                {
                    BatchId         = batchId,
                    UserId          = userId,
                    GrantedByUserId = currentUserId,
                    GrantedAt       = now,
                    IsActive        = true
                });
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"تم تحديث موافقات الوصول لأرشيف الدفعة '{batch.Name}'.";
            return RedirectToAction(nameof(ManageArchiveAccess), new { batchId });
        }

    }
}
