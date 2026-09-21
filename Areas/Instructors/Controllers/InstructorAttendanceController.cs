using EFCore.BulkExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Instructor.Attendance;

namespace QdratNew.Areas.Instructors.Controllers
{
    [Area("Instructors")]
    public class InstructorAttendanceController : BaseInstructorController
    {
        private readonly ApplicationDbContext _context;

        public InstructorAttendanceController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
            : base(userManager, scopeService)
        {
            _context = context;
        }

        // =====================================================
        // Index = دفعات المدرب فقط
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var today = DateTime.Today;

            var instructorLectures = await _context.Lecture
                .AsNoTracking()
                .Include(l => l.Batch)
                .ThenInclude(b => b.Course)
                .Include(l => l.Course)
                .Include(l => l.Section)
                .ThenInclude(s => s.Curriculum)
                .Where(l => l.InstructorId == instructorId && !l.Batch.IsArchived)
                .ToListAsync();

            var enrollments = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Select(x => new
                {
                    x.BatchId,
                    x.StudentID
                })
                .ToListAsync();

            var attendanceRecords = await (
                from a in _context.AttendanceRecords.AsNoTracking()
                join l in _context.Lecture.AsNoTracking()
                    on a.LectureId equals l.Id
                where l.InstructorId == instructorId
                select new
                {
                    a.LectureId,
                    a.StudentId,
                    a.IsPresent,
                    a.IsLateArrival,
                    a.HasEarlyLeavePermission
                }
            ).ToListAsync();

            var batchCards = instructorLectures
                .GroupBy(l => l.BatchId)
                .Select(g =>
                {
                    var firstLecture = g.First();
                    var batchId = g.Key;

                    var batchLectures = g.ToList();
                    var lectureIds = batchLectures.Select(x => x.Id).ToList();

                    var batchAttendance = attendanceRecords
                        .Where(a => lectureIds.Any(id => id == a.LectureId))
                        .ToList();

                    var totalStudents = enrollments.Count(e => e.BatchId == batchId);
                    var totalLectures = batchLectures.Count;
                    var presentCount = batchAttendance.Count(a => a.IsPresent);

                    var attendancePercentage =
                        totalStudents > 0 && totalLectures > 0
                            ? Math.Round((presentCount * 100.0) / (totalStudents * totalLectures), 1)
                            : 0;

                    return new InstructorAttendanceBatchCardVM
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

                        TotalStudents = totalStudents,
                        TotalLectures = totalLectures,
                        TodayLectures = batchLectures.Count(x => x.Date.Date == today),

                        RecordedLectures = batchLectures.Count(l => batchAttendance.Any(a => a.LectureId == l.Id)),
                        PendingLectures = batchLectures.Count(l => !batchAttendance.Any(a => a.LectureId == l.Id)),

                        PresentCount = presentCount,
                        LateArrivalCount = batchAttendance.Count(a => a.IsLateArrival),
                        EarlyLeavePermissionCount = batchAttendance.Count(a => a.HasEarlyLeavePermission),

                        AttendancePercentage = attendancePercentage,
                        LastLectureDate = batchLectures.OrderByDescending(x => x.Date).Select(x => (DateTime?)x.Date).FirstOrDefault()
                    };
                })
                .OrderByDescending(x => x.TodayLectures)
                .ThenByDescending(x => x.LastLectureDate)
                .ToList();

            var model = new InstructorAttendanceIndexVM
            {
                TotalBatches = batchCards.Count,
                TotalLectures = batchCards.Sum(x => x.TotalLectures),
                TodayLectures = batchCards.Sum(x => x.TodayLectures),
                CompletedAttendanceLectures = batchCards.Sum(x => x.RecordedLectures),
                PendingAttendanceLectures = batchCards.Sum(x => x.PendingLectures),
                BatchCards = batchCards,
                Batches = batchCards.Select(x => new InstructorAttendanceBatchOptionVM
                {
                    BatchId = x.BatchId,
                    BatchName = x.BatchName
                }).ToList()
            };

            return View(model);
        }

        // =====================================================
        // Lectures = محاضرات دفعة واحدة فقط
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Lectures(int batchId, DateTime? dateFrom, DateTime? dateTo)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var today = DateTime.Today;

            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                return NotFound("لم يتم العثور على الدفعة.");

            // Check explicit Attendance permission (works for both archived and active batches)
            var hasAttendancePermission = await _context.Set<InstructorBatchPermission>()
                .AsNoTracking()
                .AnyAsync(x => x.InstructorId == instructorId &&
                               x.BatchId == batchId &&
                               x.Feature == InstructorBatchFeature.Attendance &&
                               x.IsGranted);

            if (batch.IsArchived && !hasAttendancePermission)
                return Forbid();

            if (!hasAttendancePermission)
            {
                var hasDirectBatchAccess = await _context.Set<InstructorBatchRole>()
                    .AsNoTracking()
                    .AnyAsync(x => x.InstructorId == instructorId && x.BatchId == batchId);

                var hasCurriculumBatchAccess = await _context.Set<InstructorCurriculumBatch>()
                    .AsNoTracking()
                    .AnyAsync(x => x.InstructorId == instructorId && x.BatchId == batchId);

                var hasLectureAccess = await _context.Lecture
                    .AsNoTracking()
                    .AnyAsync(l => l.InstructorId == instructorId && l.BatchId == batchId);

                if (!hasDirectBatchAccess && !hasCurriculumBatchAccess && !hasLectureAccess)
                    return Forbid();
            }

            var lecturesQuery = _context.Lecture
                .AsNoTracking()
                .Include(l => l.Batch)
                .ThenInclude(b => b.Course)
                .Include(l => l.Course)
                .Include(l => l.Section)
                .ThenInclude(s => s.Curriculum)
                .Where(l => l.InstructorId == instructorId && l.BatchId == batchId);

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

            var totalStudents = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == batchId)
                .CountAsync();

            var attendanceRecords = await (
                from a in _context.AttendanceRecords.AsNoTracking()
                join l in _context.Lecture.AsNoTracking()
                    on a.LectureId equals l.Id
                where l.InstructorId == instructorId && l.BatchId == batchId
                select new
                {
                    a.LectureId,
                    a.StudentId,
                    a.IsPresent,
                    a.IsLateArrival,
                    a.HasEarlyLeavePermission
                }
            ).ToListAsync();

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

                    return new InstructorLectureAttendanceRowVM
                    {
                        LectureId = l.Id,
                        Title = l.Title,
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

            var model = new InstructorBatchLecturesVM
            {
                BatchId = batch.Id,
                BatchName = batch.Name,
                CourseTitle = batch.Course != null ? batch.Course.Name : "غير محدد",
                DateFrom = dateFrom,
                DateTo = dateTo,

                TotalStudents = totalStudents,
                TotalLectures = lectureRows.Count,
                TodayLectures = lectureRows.Count(x => x.IsToday),
                CompletedAttendanceLectures = lectureRows.Count(x => x.HasAttendance),
                PendingAttendanceLectures = lectureRows.Count(x => !x.HasAttendance),

                Lectures = lectureRows
            };

            return View(model);
        }

        // =====================================================
        // توافق مع روابط قديمة كانت تفتح Mark
        // =====================================================
        [HttpGet]
        public IActionResult Mark(int id)
        {
            return RedirectToAction(nameof(Record), new { lectureId = id });
        }

        // =====================================================
        // Record GET = طلاب دفعة المحاضرة فقط
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Record(int lectureId)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var lecture = await _context.Lecture
                .AsNoTracking()
                .Include(l => l.Batch)
                .ThenInclude(b => b.Course)
                .Include(l => l.Course)
                .Include(l => l.Section)
                .ThenInclude(s => s.Curriculum)
                .FirstOrDefaultAsync(l => l.Id == lectureId && l.InstructorId == instructorId);

            if (lecture == null)
                return NotFound("لم يتم العثور على المحاضرة أو لا تملك صلاحية عليها.");

            if (lecture.Batch?.IsArchived == true)
                return Forbid();

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

                    return new InstructorAttendanceStudentRowVM
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

            var model = new MarkInstructorAttendanceVM
            {
                LectureId = lecture.Id,
                BatchId = lecture.BatchId,
                LectureTitle = lecture.Title,
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

            return View(model);
        }

        // =====================================================
        // Record POST = حفظ حضور المحاضرة ثم الرجوع لمحاضرات نفس الدفعة
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Record(MarkInstructorAttendanceVM model)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            if (!ModelState.IsValid)
            {
                var validationErrors = string.Join(" | ", ModelState
                    .Where(kv => kv.Value.Errors.Count > 0)
                    .Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value.Errors.Select(e => e.ErrorMessage))}"));

                Console.WriteLine("❌ ATTENDANCE RECORD — ModelState INVALID: " + validationErrors);
                TempData["Error"] = "تعذّر حفظ بيانات المحاضرة، الرجاء إعادة المحاولة.";
                return RedirectToAction(nameof(Index));
            }

            if (model.Students == null || !model.Students.Any())
            {
                Console.WriteLine($"❌ ATTENDANCE RECORD — Students list empty for LectureId={model.LectureId}, BatchId={model.BatchId}");
                TempData["Error"] = "لم يتم إرسال بيانات الطلاب بشكل صحيح.";
                return RedirectToAction(nameof(Index));
            }

            var lecture = await _context.Lecture
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == model.LectureId && l.InstructorId == instructorId);

            if (lecture == null)
                return NotFound("لم يتم العثور على المحاضرة أو لا تملك صلاحية عليها.");

            var batchIsArchived = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == lecture.BatchId)
                .Select(b => b.IsArchived)
                .FirstOrDefaultAsync();

            if (batchIsArchived)
                return Forbid();

            var validStudents = await (
                from e in _context.StudentBatchEnrollments
                join s in _context.Students on e.StudentID equals s.StudentID
                join u in _context.Users on s.UserId equals u.Id
                where e.BatchId == lecture.BatchId
                      && u.IsActive == true
                select new
                {
                    StudentId = s.StudentID
                }
            ).ToListAsync();

            var filteredStudents = model.Students
                .Where(x => validStudents.Any(v => v.StudentId == x.StudentId))
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

            var existingMap = existingRecords.ToDictionary(x => x.StudentId, x => x);

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

            // تسجيل وقت البدء الفعلي عند أول تسجيل حضور للمحاضرة (first one wins)
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
                    lectureToUpdate.StartedByRole = roles.FirstOrDefault() ?? "Instructor";
                }
            }

            var affectedRows = newRecords.Count + existingRecords.Count;

            if (affectedRows > 20)
            {
                await _context.BulkSaveChangesAsync();
            }
            else
            {
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "تم حفظ الحضور والانصراف بنجاح.";
            return RedirectToAction(nameof(Lectures), new { batchId = lecture.BatchId });
        }

        // =====================================================
        // StartLecture POST = تسجيل وقت البدء الفعلي يدوياً
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartLecture(int lectureId)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var lecture = await _context.Lecture
                .FirstOrDefaultAsync(l => l.Id == lectureId && l.InstructorId == instructorId);

            if (lecture == null)
                return NotFound("لم يتم العثور على المحاضرة أو لا تملك صلاحية عليها.");

            var batchIsArchivedSL = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == lecture.BatchId)
                .Select(b => b.IsArchived)
                .FirstOrDefaultAsync();

            if (batchIsArchivedSL)
                return Forbid();

            // first one wins — لا يُعاد الكتابة إذا سُجِّل وقت البدء مسبقاً
            if (lecture.ActualStartTime != null)
            {
                TempData["Info"] = "تم تسجيل وقت البدء الفعلي مسبقاً ولا يمكن تغييره.";
                return RedirectToAction(nameof(Record), new { lectureId });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var roles = currentUser != null
                ? await _userManager.GetRolesAsync(currentUser)
                : new List<string>();

            lecture.ActualStartTime = DateTime.UtcNow;
            lecture.StartedByUserId = currentUser?.Id;
            lecture.StartedByRole = roles.FirstOrDefault() ?? "Instructor";

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم تسجيل وقت بدء المحاضرة الفعلي بنجاح.";
            return RedirectToAction(nameof(Record), new { lectureId });
        }

        // =====================================================
        // EndLecture POST = تسجيل وقت الانتهاء الفعلي يدوياً
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EndLecture(int lectureId)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var lecture = await _context.Lecture
                .FirstOrDefaultAsync(l => l.Id == lectureId && l.InstructorId == instructorId);

            if (lecture == null)
                return NotFound("لم يتم العثور على المحاضرة أو لا تملك صلاحية عليها.");

            var batchIsArchivedEL = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == lecture.BatchId)
                .Select(b => b.IsArchived)
                .FirstOrDefaultAsync();

            if (batchIsArchivedEL)
                return Forbid();

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
            lecture.EndedByRole = roles.FirstOrDefault() ?? "Instructor";

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم تسجيل وقت انتهاء المحاضرة الفعلي بنجاح.";
            return RedirectToAction(nameof(Record), new { lectureId });
        }

        // =====================================================
        // تقرير محاضرة واحدة
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> LectureReport(int lectureId)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var lecture = await _context.Lecture
                .AsNoTracking()
                .Include(l => l.Batch)
                .ThenInclude(b => b.Course)
                .Include(l => l.Course)
                .Include(l => l.Section)
                .ThenInclude(s => s.Curriculum)
                .FirstOrDefaultAsync(l => l.Id == lectureId && l.InstructorId == instructorId);

            if (lecture == null)
                return NotFound("لم يتم العثور على المحاضرة أو لا تملك صلاحية عليها.");

            if (lecture.Batch?.IsArchived == true)
                return Forbid();

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

                    return new InstructorAttendanceStudentRowVM
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

            var model = new MarkInstructorAttendanceVM
            {
                LectureId = lecture.Id,
                BatchId = lecture.BatchId,
                LectureTitle = lecture.Title,
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

            return View(model);
        }
    }
}