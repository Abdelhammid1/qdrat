using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.ViewModels.Lecture;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin,Owner,Employee,Developer")]
    public class LecturesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LecturesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // Index = عرض الدفعات أولًا
        // =====================================================
        [AdminPermission("Lectures", "Read")]
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            var batches = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .Where(b => !b.IsDeleted && !b.IsArchived)
                .OrderByDescending(b => b.Id)
                .ToListAsync();

            var lectures = await _context.Lecture
                .AsNoTracking()
                .Include(l => l.Batch)
                .ThenInclude(b => b.Course)
                .Include(l => l.Course)
                .Include(l => l.Instructor)
                .Include(l => l.Section)
                .ToListAsync();

            var enrollments = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Select(x => new
                {
                    x.BatchId,
                    x.StudentID
                })
                .ToListAsync();

            var batchCards = batches
         .Select(batch =>
         {
             var batchLectures = lectures
                 .Where(l => l.BatchId == batch.Id)
                 .ToList();

             var instructors = batchLectures
                 .Where(l => l.Instructor != null)
                 .Select(l => l.Instructor.FullName)
                 .Distinct()
                 .ToList();

             var instructorSummary = instructors.Count == 0
                 ? "لا يوجد مدرب"
                 : instructors.Count == 1
                     ? instructors.First()
                     : $"{instructors.Count} مدربين";

             return new LectureBatchCardViewModel
             {
                 BatchId = batch.Id,
                 BatchName = batch.Name,
                 CourseName = batch.Course != null ? batch.Course.Name : "غير محدد",

                 TotalStudents = enrollments.Count(e => e.BatchId == batch.Id),
                 TotalLectures = batchLectures.Count,
                 TodayLectures = batchLectures.Count(l => l.Date.Date == today),
                 UpcomingLectures = batchLectures.Count(l => l.Date.Date > today),

                 InstructorSummary = instructorSummary,

                 LastLectureDate = batchLectures
                     .OrderByDescending(l => l.Date)
                     .Select(l => (DateTime?)l.Date)
                     .FirstOrDefault(),

                 NextLectureDate = batchLectures
                     .Where(l => l.Date.Date >= today)
                     .OrderBy(l => l.Date)
                     .Select(l => (DateTime?)l.Date)
                     .FirstOrDefault()
             };
         })
         .OrderByDescending(x => x.LastLectureDate.HasValue)
         .ThenByDescending(x => x.LastLectureDate)
         .ThenByDescending(x => x.BatchId)
         .ToList();

            var model = new LectureBatchesIndexViewModel
            {
                TotalBatches = batchCards.Count,
                TotalLectures = batchCards.Sum(x => x.TotalLectures),
                TodayLectures = batchCards.Sum(x => x.TodayLectures),
                UpcomingLectures = batchCards.Sum(x => x.UpcomingLectures),
                Batches = batchCards
            };

            return View(model);
        }

        // =====================================================
        // Lectures = محاضرات دفعة واحدة
        // =====================================================
        [AdminPermission("Lectures", "Read")]
        public async Task<IActionResult> Lectures(int batchId)
        {
            var today = DateTime.Today;

            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                return NotFound();

            var lectures = await _context.Lecture
                .AsNoTracking()
                .Include(l => l.Course)
                .Include(l => l.Instructor)
                .Include(l => l.Section)
                .Include(l => l.Batch)
                .Where(l => l.BatchId == batchId)
                .OrderByDescending(l => l.Date.Date == today)
                .ThenBy(l => l.Date.Date >= today ? 0 : 1)
                .ThenBy(l => l.Date)
                .Select(l => new LectureListViewModel
                {
                    Id = l.Id,
                    Title = l.Title,
                    Date = l.Date,
                    ScheduledTime = l.ScheduledTime,
                    DurationMinutes = l.DurationMinutes,
                    ScheduledEndTime = l.ScheduledEndTime,
                    Location = l.Location,
                    InstructorName = l.Instructor != null ? l.Instructor.FullName : "غير محدد",
                    IsManualInstructorAssignment = l.InstructorAssignmentSource == LectureInstructorAssignmentSource.Manual,
                    SectionTitle = l.Section != null ? l.Section.Title : "غير محدد",
                    CourseName = l.Course != null
                        ? l.Course.Name
                        : batch.Course != null
                            ? batch.Course.Name
                            : "غير محدد",
                    BatchName = l.Batch != null ? l.Batch.Name : batch.Name
                })
                .ToListAsync();

            var totalStudents = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(x => x.BatchId == batchId)
                .CountAsync();

            var model = new BatchLecturesPageViewModel
            {
                BatchId = batch.Id,
                BatchName = batch.Name,
                CourseName = batch.Course != null ? batch.Course.Name : "غير محدد",

                TotalStudents = totalStudents,
                TotalLectures = lectures.Count,
                TodayLectures = lectures.Count(l => l.Date.Date == today),
                UpcomingLectures = lectures.Count(l => l.Date.Date > today),

                Lectures = lectures
            };

            return View(model);
        }

        // =====================================================
        // Create GET
        // =====================================================
        [AdminPermission("Lectures", "Create")]
        public async Task<IActionResult> Create(int? batchId)
        {
            var vm = new LectureFormViewModel
            {
                Courses = await _context.Courses
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync(),

                Sections = await _context.Sections
                    .AsNoTracking()
                    .OrderBy(s => s.Title)
                    .ToListAsync(),

                Batches = await _context.Batches
                    .AsNoTracking()
                    .Where(b => !b.IsDeleted && !b.IsArchived)
                    .OrderBy(b => b.Name)
                    .ToListAsync(),

                BatchId = batchId
            };

            await LoadInstructorListsAsync(vm, batchId);

            return View(vm);
        }

        // ✅ يحمّل قائمتي المدربين: المرتبطين رسميًا بالمنهج/الدفعة (Instructors) وكل المدربين النشطين (AllActiveInstructors)
        private async Task LoadInstructorListsAsync(LectureFormViewModel vm, int? batchId)
        {
            vm.AllActiveInstructors = await _context.Instructors
                .AsNoTracking()
                .Where(i => i.IsActive)
                .OrderBy(i => i.FullName)
                .ToListAsync();

            var linkedInstructors = batchId.HasValue
                ? await (
                    from link in _context.InstructorCurriculumBatches.AsNoTracking()
                    join section in _context.Sections.AsNoTracking() on link.CurriculumId equals section.CurriculumId
                    join instructor in _context.Instructors.AsNoTracking() on link.InstructorId equals instructor.Id
                    where link.BatchId == batchId.Value && instructor.IsActive
                    select instructor
                  ).Distinct().OrderBy(i => i.FullName).ToListAsync()
                : new List<Instructor>();

            // ✅ لا يوجد ربط رسمي متاح (لا دفعة محددة بعد) — لا نكسر الشاشة، نعرض كل المدربين كقائمة افتراضية
            vm.Instructors = linkedInstructors.Count > 0 ? linkedInstructors : vm.AllActiveInstructors;
        }

        // =====================================================
        // Create POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Lectures", "Create")]
        public async Task<IActionResult> Create(LectureFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.Courses = await _context.Courses
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                vm.Sections = await _context.Sections
                    .AsNoTracking()
                    .OrderBy(s => s.Title)
                    .ToListAsync();

                vm.Batches = await _context.Batches
                    .AsNoTracking()
                    .Where(b => !b.IsDeleted && !b.IsArchived)
                    .OrderBy(b => b.Name)
                    .ToListAsync();

                await LoadInstructorListsAsync(vm, vm.BatchId);

                return View(vm);
            }

            if (!vm.BatchId.HasValue || vm.BatchId.Value == 0)
            {
                ModelState.AddModelError(nameof(vm.BatchId), "يجب اختيار الدفعة.");

                vm.Courses = await _context.Courses.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
                vm.Sections = await _context.Sections.AsNoTracking().OrderBy(s => s.Title).ToListAsync();
                vm.Batches = await _context.Batches.AsNoTracking().Where(b => !b.IsDeleted && !b.IsArchived).OrderBy(b => b.Name).ToListAsync();
                await LoadInstructorListsAsync(vm, vm.BatchId);

                return View(vm);
            }

            TimeSpan? scheduledEnd = null;
            if (vm.ScheduledTime.HasValue && vm.DurationMinutes.HasValue && vm.DurationMinutes.Value > 0)
                scheduledEnd = vm.ScheduledTime.Value.Add(TimeSpan.FromMinutes(vm.DurationMinutes.Value));
            else if (vm.ScheduledEndTime.HasValue)
                scheduledEnd = vm.ScheduledEndTime;

            var lecture = new Lecture
            {
                Title = vm.Title,
                Location = vm.Location,
                Date = vm.Date,
                ScheduledTime = vm.ScheduledTime,
                DurationMinutes = vm.DurationMinutes,
                ScheduledEndTime = scheduledEnd,
                CourseId = vm.CourseId,
                SectionId = vm.SectionId,
                BatchId = vm.BatchId.Value,
                InstructorId = vm.InstructorId,
                InstructorAssignmentSource = await IsLinkedInstructorAsync(vm.InstructorId, vm.BatchId.Value, vm.SectionId)
                    ? LectureInstructorAssignmentSource.Auto
                    : LectureInstructorAssignmentSource.Manual
            };

            _context.Lecture.Add(lecture);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Lectures), new { batchId = lecture.BatchId });
        }

        // ✅ هل المدرب المختار مرتبط رسميًا (عبر InstructorCurriculumBatch) بمنهج قسم المحاضرة ودفعتها؟
        private async Task<bool> IsLinkedInstructorAsync(int instructorId, int batchId, int sectionId)
        {
            return await _context.InstructorCurriculumBatches
                .AsNoTracking()
                .AnyAsync(x => x.InstructorId == instructorId && x.BatchId == batchId
                    && _context.Sections.Any(s => s.Id == sectionId && s.CurriculumId == x.CurriculumId));
        }

        // =====================================================
        // Edit GET
        // =====================================================
        [AdminPermission("Lectures", "Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var lecture = await _context.Lecture.FindAsync(id);
            if (lecture == null)
                return NotFound();

            var vm = new LectureEditViewModel
            {
                Id = lecture.Id,
                Title = lecture.Title,
                Date = lecture.Date,
                ScheduledTime = lecture.ScheduledTime,
                DurationMinutes = lecture.DurationMinutes,
                ScheduledEndTime = lecture.ScheduledEndTime,
                Location = lecture.Location,
                InstructorId = lecture.InstructorId,
                SectionId = lecture.SectionId,
                CourseId = lecture.CourseId,
                BatchId = lecture.BatchId,

                Courses = await _context.Courses
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.Name
                    })
                    .ToListAsync(),

                Sections = await _context.Sections
                    .AsNoTracking()
                    .OrderBy(s => s.Title)
                    .Select(s => new SelectListItem
                    {
                        Value = s.Id.ToString(),
                        Text = s.Title
                    })
                    .ToListAsync(),

                Batches = await _context.Batches
                    .AsNoTracking()
                    .Where(b => !b.IsDeleted)
                    .OrderBy(b => b.Name)
                    .Select(b => new SelectListItem
                    {
                        Value = b.Id.ToString(),
                        Text = b.Name
                    })
                    .ToListAsync()
            };

            await LoadInstructorSelectListsAsync(vm, lecture.BatchId);

            return View(vm);
        }

        // ✅ نسخة SelectListItem من قوائم المدربين (لشاشة Edit التي تستخدم SelectListItem بدل الكيان مباشرة)
        private async Task LoadInstructorSelectListsAsync(LectureEditViewModel vm, int? batchId)
        {
            vm.AllActiveInstructors = await _context.Instructors
                .AsNoTracking()
                .Where(i => i.IsActive)
                .OrderBy(i => i.FullName)
                .Select(i => new SelectListItem { Value = i.Id.ToString(), Text = i.FullName })
                .ToListAsync();

            var linkedInstructorEntities = batchId.HasValue
                ? await (
                    from link in _context.InstructorCurriculumBatches.AsNoTracking()
                    join section in _context.Sections.AsNoTracking() on link.CurriculumId equals section.CurriculumId
                    join instructor in _context.Instructors.AsNoTracking() on link.InstructorId equals instructor.Id
                    where link.BatchId == batchId.Value && instructor.IsActive
                    select instructor
                  ).Distinct().OrderBy(i => i.FullName).ToListAsync()
                : new List<Instructor>();

            var linkedInstructors = linkedInstructorEntities
                .Select(i => new SelectListItem { Value = i.Id.ToString(), Text = i.FullName })
                .ToList();

            vm.Instructors = linkedInstructors.Count > 0 ? linkedInstructors : vm.AllActiveInstructors;
        }

        // =====================================================
        // Edit POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Lectures", "Edit")]
        public async Task<IActionResult> Edit(int id, LectureEditViewModel vm)
        {
            if (id != vm.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                vm.Courses = await _context.Courses
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                    .ToListAsync();

                vm.Sections = await _context.Sections
                    .AsNoTracking()
                    .OrderBy(s => s.Title)
                    .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title })
                    .ToListAsync();

                vm.Batches = await _context.Batches
                    .AsNoTracking()
                    .Where(b => !b.IsDeleted && !b.IsArchived)
                    .OrderBy(b => b.Name)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                    .ToListAsync();

                await LoadInstructorSelectListsAsync(vm, vm.BatchId);

                return View(vm);
            }

            if (!vm.BatchId.HasValue || vm.BatchId.Value == 0)
            {
                ModelState.AddModelError(nameof(vm.BatchId), "يجب اختيار الدفعة.");
                return View(vm);
            }

            var lecture = await _context.Lecture.FindAsync(id);
            if (lecture == null)
                return NotFound();

            TimeSpan? scheduledEnd = null;
            if (vm.ScheduledTime.HasValue && vm.DurationMinutes.HasValue && vm.DurationMinutes.Value > 0)
                scheduledEnd = vm.ScheduledTime.Value.Add(TimeSpan.FromMinutes(vm.DurationMinutes.Value));
            else if (vm.ScheduledEndTime.HasValue)
                scheduledEnd = vm.ScheduledEndTime;

            lecture.Title = vm.Title;
            lecture.Date = vm.Date;
            lecture.ScheduledTime = vm.ScheduledTime;
            lecture.DurationMinutes = vm.DurationMinutes;
            lecture.ScheduledEndTime = scheduledEnd;
            lecture.Location = vm.Location;
            lecture.InstructorId = vm.InstructorId;
            lecture.SectionId = vm.SectionId;
            lecture.CourseId = vm.CourseId;
            lecture.BatchId = vm.BatchId.Value;
            lecture.InstructorAssignmentSource = await IsLinkedInstructorAsync(vm.InstructorId, vm.BatchId.Value, vm.SectionId)
                ? LectureInstructorAssignmentSource.Auto
                : LectureInstructorAssignmentSource.Manual;

            _context.Update(lecture);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Lectures), new { batchId = lecture.BatchId });
        }

        // =====================================================
        // Details
        // =====================================================
        [AdminPermission("Lectures", "Read")]
        public async Task<IActionResult> Details(int id)
        {
            var lecture = await _context.Lecture
                .AsNoTracking()
                .Include(l => l.Instructor)
                .Include(l => l.Section)
                .Include(l => l.Course)
                .Include(l => l.Batch)
                .Include(l => l.Lessons)
                .Include(l => l.AttendanceRecords)
                    .ThenInclude(a => a.Student)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lecture == null)
                return NotFound();

            var totalStudents = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .CountAsync(e => e.BatchId == lecture.BatchId);

            var attendanceRows = lecture.AttendanceRecords
                .Where(a => a.Student != null)
                .Select(a => new LectureAttendanceStudentRow
                {
                    StudentId = a.StudentId,
                    StudentName = a.Student.FullName ?? "—",
                    NationalId = a.Student.NationalID ?? "—",
                    IsPresent = a.IsPresent,
                    IsLateArrival = a.IsLateArrival,
                    ActualArrivalTime = a.ActualArrivalTime,
                    Notes = a.Notes,
                    RecordedAt = a.RecordedAt
                })
                .OrderBy(r => r.StudentName)
                .ToList();

            var model = new LectureDetailsViewModel
            {
                Id = lecture.Id,
                Title = lecture.Title,
                Location = lecture.Location ?? "—",
                Date = lecture.Date,
                ScheduledTime = lecture.ScheduledTime,
                ScheduledEndTime = lecture.ScheduledEndTime,
                DurationMinutes = lecture.DurationMinutes,
                ActualStartTime = lecture.ActualStartTime,
                ActualEndTime = lecture.ActualEndTime,
                StartedByRole = lecture.StartedByRole,
                EndedByRole = lecture.EndedByRole,
                InstructorName = lecture.Instructor?.FullName ?? "غير محدد",
                SectionTitle = lecture.Section?.Title ?? "غير محدد",
                CourseName = lecture.Course?.Name ?? "غير محدد",
                BatchName = lecture.Batch?.Name ?? "غير محدد",
                BatchId = lecture.BatchId,
                TotalStudents = totalStudents,
                PresentCount = attendanceRows.Count(r => r.IsPresent),
                AbsentCount = attendanceRows.Count(r => !r.IsPresent),
                AttendanceRecords = attendanceRows,
                LessonTitles = lecture.Lessons.Select(l => l.Title).ToList()
            };

            return View(model);
        }

        // =====================================================
        // Delete GET
        // =====================================================
        [AdminPermission("Lectures", "Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var lecture = await _context.Lecture
                .Include(l => l.Course)
                .Include(l => l.Instructor)
                .Include(l => l.Section)
                .Include(l => l.Batch)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lecture == null)
                return NotFound();

            return View(lecture);
        }

        // =====================================================
        // Delete POST
        // =====================================================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AdminPermission("Lectures", "Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var lecture = await _context.Lecture.FindAsync(id);
            if (lecture == null)
                return RedirectToAction(nameof(Index));

            var batchId = lecture.BatchId;

            _context.Lecture.Remove(lecture);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Lectures), new { batchId });
        }
    }
}
