using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Instructor;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class InstructorsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public InstructorsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Admin/Instructors
        // GET: Admin/Instructors
        public async Task<IActionResult> Index()
        {
            var instructors = await _context.Instructors
                .AsNoTracking()
                .OrderBy(i => i.FullName)
                .Select(i => new InstructorViewModel
                {
                    Id = i.Id,
                    FullName = i.FullName,
                    Email = i.Email,
                    PhoneNumber = i.PhoneNumber,
                    WhatsAppNumber = i.WhatsAppNumber,
                    Gender = i.Gender,
                    Specialization = i.Specialization,
                    IsActive = i.IsActive
                })
                .ToListAsync();

            var curriculumLinks = await _context.CurriculumInstructors
                .Include(ci => ci.Curriculum)
                .AsNoTracking()
                .ToListAsync();

            var batchLinks = await _context.InstructorCurriculumBatches
                .Include(x => x.Batch)
                .Include(x => x.Curriculum)
                .AsNoTracking()
                .ToListAsync();

            foreach (var inst in instructors)
            {
                inst.LinkedCurriculums = curriculumLinks
                    .Where(ci => ci.InstructorId == inst.Id)
                    .Select(ci => new CurriculumLinkVm { CurriculumId = ci.CurriculumId, CurriculumTitle = ci.Curriculum.Title })
                    .ToList();

                inst.LinkedBatches = batchLinks
                    .Where(x => x.InstructorId == inst.Id)
                    .Select(x => new BatchLinkVm { BatchId = x.BatchId, BatchName = x.Batch.Name, CurriculumTitle = x.Curriculum.Title })
                    .ToList();
            }

            ViewBag.AllCurriculums = await _context.Curriculums
                .OrderBy(c => c.Title)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            return View(instructors);
        }
        // GET: Admin/Instructors/Create
        public async Task<IActionResult> Create()
        {
            var usersWithInstructorRole = await (from user in _context.Users
                                                 join userRole in _context.UserRoles on user.Id equals userRole.UserId
                                                 join role in _context.Roles on userRole.RoleId equals role.Id
                                                 where role.Name == "Instructor"
                                                 select user).ToListAsync();

            var usedUserIds = await _context.Instructors
                .Where(i => i.UserId != null)
                .Select(i => i.UserId)
                .ToListAsync();

            var availableUsers = usersWithInstructorRole
                .Where(u => !usedUserIds.Contains(u.Id))
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = u.Email
                })
                .ToList();

            var vm = new InstructorViewModel
            {
                Users = availableUsers,
                Genders = new List<SelectListItem>
        {
            new SelectListItem { Value = "Male", Text = "ذكر" },
            new SelectListItem { Value = "Female", Text = "أنثى" }
        }
            };

            return View(vm);
        }


        // POST: Admin/Instructors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InstructorViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                var usersWithInstructorRole = await (from user in _context.Users
                                                     join userRole in _context.UserRoles on user.Id equals userRole.UserId
                                                     join role in _context.Roles on userRole.RoleId equals role.Id
                                                     where role.Name == "Instructor"
                                                     select user).ToListAsync();

                var usedUserIds = await _context.Instructors
                    .Where(i => i.UserId != null)
                    .Select(i => i.UserId)
                    .ToListAsync();

                var availableUsers = usersWithInstructorRole
                    .Where(u => !usedUserIds.Contains(u.Id))
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id,
                        Text = u.Email
                    })
                    .ToList();

                vm.Users = availableUsers;
                return View(vm);
            }

            var instructor = new Instructor
            {
                FullName = vm.FullName,
                Email = vm.Email,
                PhoneNumber = vm.PhoneNumber,
                WhatsAppNumber = vm.WhatsAppNumber,
                Gender = vm.Gender ?? GenderType.Male,
                Specialization = vm.Specialization,
                IsActive = vm.IsActive,
                NationalID = vm.NationalID,
                UserId = vm.UserId
            };


            _context.Instructors.Add(instructor);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var instructor = await _context.Instructors.FindAsync(id);
            if (instructor == null) return NotFound();

            var usersWithInstructorRole = await (from user in _context.Users
                                                 join userRole in _context.UserRoles on user.Id equals userRole.UserId
                                                 join role in _context.Roles on userRole.RoleId equals role.Id
                                                 where role.Name == "Instructor"
                                                 select user).ToListAsync();

            var usedUserIds = await _context.Instructors
                .Where(i => i.UserId != null && i.Id != id)
                .Select(i => i.UserId)
                .ToListAsync();

            var availableUsers = usersWithInstructorRole
                .Where(u => !usedUserIds.Contains(u.Id) || u.Id == instructor.UserId)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = u.Email
                }).ToList();

            var vm = new InstructorViewModel
            {
                Id = instructor.Id,
                FullName = instructor.FullName,
                Email = instructor.Email,
                PhoneNumber = instructor.PhoneNumber,
                WhatsAppNumber = instructor.WhatsAppNumber,
                Gender = instructor.Gender,
                Specialization = instructor.Specialization,
                IsActive = instructor.IsActive,
                NationalID = instructor.NationalID,
                UserId = instructor.UserId,
                Users = availableUsers,
                Genders = new List<SelectListItem>
        {
            new SelectListItem { Value = "Male", Text = "ذكر", Selected = instructor.Gender == GenderType.Male },
            new SelectListItem { Value = "Female", Text = "أنثى", Selected = instructor.Gender == GenderType.Female }
        }
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, InstructorViewModel vm)
        {
            if (id != vm.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                var usersWithInstructorRole = await (from user in _context.Users
                                                     join userRole in _context.UserRoles on user.Id equals userRole.UserId
                                                     join role in _context.Roles on userRole.RoleId equals role.Id
                                                     where role.Name == "Instructor"
                                                     select user).ToListAsync();

                var usedUserIds = await _context.Instructors
                    .Where(i => i.UserId != null && i.Id != id)
                    .Select(i => i.UserId)
                    .ToListAsync();

                var availableUsers = usersWithInstructorRole
                    .Where(u => !usedUserIds.Contains(u.Id) || u.Id == vm.UserId)
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id,
                        Text = u.Email
                    }).ToList();

                vm.Users = availableUsers;

                vm.Genders = new List<SelectListItem>
        {
            new SelectListItem { Value = "Male", Text = "ذكر", Selected = vm.Gender == GenderType.Male },
            new SelectListItem { Value = "Female", Text = "أنثى", Selected = vm.Gender == GenderType.Female }
        };

                return View(vm);
            }

            var instructor = await _context.Instructors.FindAsync(id);
            if (instructor == null) return NotFound();

            instructor.FullName = vm.FullName;
            instructor.Email = vm.Email;
            instructor.PhoneNumber = vm.PhoneNumber;
            instructor.WhatsAppNumber = vm.WhatsAppNumber;
            instructor.Gender = vm.Gender ?? GenderType.Male;
            instructor.Specialization = vm.Specialization;
            instructor.IsActive = vm.IsActive;
            instructor.NationalID = vm.NationalID;
            instructor.UserId = vm.UserId;

            _context.Update(instructor);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }



        // GET: Admin/Instructors/Details/5
        // GET: Admin/Instructors/Details/5
        public async Task<IActionResult> Details(int id)
        {
            DateTime today = DateTime.Today;
            DateTime now = DateTime.Now;
            DateTime weekStart = today.AddDays(-6);
            DateTime monthStart = new DateTime(today.Year, today.Month, 1);
            DateTime nextMonthStart = monthStart.AddMonths(1);

            var instructor = await _context.Instructors
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);

            if (instructor == null)
                return NotFound();

            List<InstructorLectureProjection> lectures = await (
                from lecture in _context.Lecture.AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on lecture.BatchId equals batch.Id
                where lecture.InstructorId == id
                select new InstructorLectureProjection
                {
                    LectureId = lecture.Id,
                    Title = lecture.Title,
                    Date = lecture.Date,
                    BatchId = lecture.BatchId,
                    BatchName = batch.Name
                })
                .OrderByDescending(x => x.Date)
                .ToListAsync();

            List<InstructorStudentProjection> students = await (
                from lecture in _context.Lecture.AsNoTracking()
                join enrollment in _context.StudentBatchEnrollments.AsNoTracking()
                    on lecture.BatchId equals enrollment.BatchId
                join student in _context.Students.AsNoTracking()
                    on enrollment.StudentID equals student.StudentID
                join batch in _context.Batches.AsNoTracking()
                    on enrollment.BatchId equals batch.Id
                where lecture.InstructorId == id
                select new InstructorStudentProjection
                {
                    StudentId = student.StudentID,
                    FullName = student.FullName,
                    BatchId = batch.Id,
                    BatchName = batch.Name
                })
                .ToListAsync();

            students = students
                .GroupBy(x => x.StudentId)
                .Select(g => g.First())
                .ToList();

            List<InstructorAttendanceProjection> attendanceRecords = await (
                from attendance in _context.AttendanceRecords.AsNoTracking()
                join lecture in _context.Lecture.AsNoTracking()
                    on attendance.LectureId equals lecture.Id
                where lecture.InstructorId == id
                select new InstructorAttendanceProjection
                {
                    LectureId = attendance.LectureId,
                    StudentId = attendance.StudentId,
                    IsPresent = attendance.IsPresent,
                    RecordedAt = attendance.RecordedAt
                })
                .ToListAsync();

            List<InstructorExamProjection> exams = await (
                from assignment in _context.ExamAssignmentsToBatches.AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on assignment.BatchId equals batch.Id
                where assignment.CreatedByInstructorId.HasValue
                      && assignment.CreatedByInstructorId.Value == id
                select new InstructorExamProjection
                {
                    ExamAssignmentId = assignment.Id,
                    Title = assignment.Title,
                    BatchId = assignment.BatchId,
                    BatchName = batch.Name,
                    CreatedAt = assignment.CreatedAt,
                    ScheduledDate = assignment.ScheduledDate
                })
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            List<InstructorExamStatusProjection> examStatuses = await (
                from status in _context.ExamStudentStatuses.AsNoTracking()
                join assignment in _context.ExamAssignmentsToBatches.AsNoTracking()
                    on status.ExamAssignmentId equals (int?)assignment.Id
                where assignment.CreatedByInstructorId.HasValue
                      && assignment.CreatedByInstructorId.Value == id
                select new InstructorExamStatusProjection
                {
                    ExamAssignmentId = assignment.Id,
                    StudentId = status.StudentId,
                    IsSubmitted = status.IsSubmitted,
                    Score = status.Score
                })
                .ToListAsync();

            List<InstructorHomeworkProjection> homeworks = new List<InstructorHomeworkProjection>();
            List<InstructorHomeworkStudentProjection> homeworkStudents = new List<InstructorHomeworkStudentProjection>();

            if (!string.IsNullOrWhiteSpace(instructor.UserId))
            {
                homeworks = await (
                    from homeworkSet in _context.HomeworkSets.AsNoTracking()
                    join batch in _context.Batches.AsNoTracking()
                        on homeworkSet.BatchId equals batch.Id
                    where homeworkSet.AssignedByUserId == instructor.UserId
                    select new InstructorHomeworkProjection
                    {
                        HomeworkSetId = homeworkSet.Id,
                        Title = homeworkSet.Title,
                        BatchId = homeworkSet.BatchId,
                        BatchName = batch.Name,
                        CreatedAt = homeworkSet.CreatedAt,
                        EndAt = homeworkSet.EndAt
                    })
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync();

                homeworkStudents = await (
                    from homeworkSet in _context.HomeworkSets.AsNoTracking()
                    join homeworkSetStudent in _context.HomeworkSetStudents.AsNoTracking()
                        on homeworkSet.Id equals homeworkSetStudent.HomeworkSetId
                    where homeworkSet.AssignedByUserId == instructor.UserId
                    select new InstructorHomeworkStudentProjection
                    {
                        HomeworkSetId = homeworkSet.Id,
                        StudentId = homeworkSetStudent.StudentId,
                        IsSubmitted = homeworkSetStudent.IsSubmitted,
                        Score = homeworkSetStudent.Score
                    })
                    .ToListAsync();
            }

            int lecturesThisMonth = lectures.Count(x => x.Date >= monthStart && x.Date < nextMonthStart);
            int lecturesThisWeek = lectures.Count(x => x.Date >= weekStart && x.Date < today.AddDays(1));

            int attendanceTotal = attendanceRecords.Count;
            int attendancePresent = attendanceRecords.Count(x => x.IsPresent);
            double attendancePercent = CalculateInstructorPercent(attendancePresent, attendanceTotal);

            int examAssignedCount = examStatuses.Count;
            int examSubmittedCount = examStatuses.Count(x => x.IsSubmitted);
            double examParticipationPercent = CalculateInstructorPercent(examSubmittedCount, examAssignedCount);

            List<InstructorExamStatusProjection> scoredExamStatuses = examStatuses
                .Where(x => x.Score.HasValue)
                .ToList();

            double examAverageScore = scoredExamStatuses.Count > 0
                ? Math.Round(scoredExamStatuses.Average(x => x.Score!.Value), 2)
                : 0;

            int homeworkAssignedCount = homeworkStudents.Count;
            int homeworkSubmittedCount = homeworkStudents.Count(x => x.IsSubmitted);
            double homeworkSubmissionPercent = CalculateInstructorPercent(homeworkSubmittedCount, homeworkAssignedCount);

            double participationPercent = Math.Round(
                (attendancePercent * 0.50) +
                (examParticipationPercent * 0.25) +
                (homeworkSubmissionPercent * 0.25),
                2);

            double contentActivityScore = Math.Min(100, (lecturesThisMonth * 8) + (exams.Count * 12) + (homeworks.Count * 10));

            double activityScore = Math.Round(
                (contentActivityScore * 0.45) +
                (participationPercent * 0.35) +
                (examAverageScore * 0.20),
                2);

            InstructorDetailsDashboardViewModel model = new InstructorDetailsDashboardViewModel
            {
                Id = instructor.Id,
                FullName = instructor.FullName,
                NationalID = instructor.NationalID,
                Email = instructor.Email,
                PhoneNumber = instructor.PhoneNumber,
                WhatsAppNumber = instructor.WhatsAppNumber,
                Gender = instructor.Gender,
                GenderText = instructor.Gender == GenderType.Male ? "ذكر" : "أنثى",
                Specialization = instructor.Specialization,
                IsActive = instructor.IsActive,
                UserId = instructor.UserId,
                AvatarText = BuildInstructorAvatar(instructor.FullName),
                StatusText = instructor.IsActive ? "نشط" : "غير نشط",
                ActivityScore = activityScore,
                ActivityLabel = ResolveInstructorActivityLabel(activityScore),
                ActivityCssClass = ResolveInstructorActivityCss(activityScore),
                BatchesCount = lectures.Select(x => x.BatchId).Distinct().Count(),
                StudentsCount = students.Count,
                LecturesThisMonth = lecturesThisMonth,
                LecturesThisWeek = lecturesThisWeek,
                ExamsCreatedCount = exams.Count,
                HomeworksCreatedCount = homeworks.Count,
                AttendancePercent = attendancePercent,
                ExamAverageScore = examAverageScore,
                HomeworkSubmissionPercent = homeworkSubmissionPercent,
                ParticipationPercent = participationPercent,
                AttendanceDeltaText = BuildInstructorDeltaText(attendancePercent, 85, "عن هدف الحضور"),
                ExamDeltaText = BuildInstructorDeltaText(examAverageScore, 80, "عن هدف الاختبارات"),
                HomeworkDeltaText = BuildInstructorDeltaText(homeworkSubmissionPercent, 90, "عن هدف الواجبات")
            };

            model.Batches = lectures
                .GroupBy(x => new { x.BatchId, x.BatchName })
                .Select(g =>
                {
                    List<InstructorLectureProjection> batchLectures = g.ToList();

                    int batchLectureCount = batchLectures.Count;

                    int batchStudentsCount = students.Count(x => x.BatchId == g.Key.BatchId);

                    int batchAttendanceTotal = 0;
                    int batchAttendancePresent = 0;

                    foreach (InstructorLectureProjection lecture in batchLectures)
                    {
                        List<InstructorAttendanceProjection> lectureAttendance = attendanceRecords
                            .Where(x => x.LectureId == lecture.LectureId)
                            .ToList();

                        batchAttendanceTotal += lectureAttendance.Count;
                        batchAttendancePresent += lectureAttendance.Count(x => x.IsPresent);
                    }

                    double batchAttendancePercent = CalculateInstructorPercent(batchAttendancePresent, batchAttendanceTotal);

                    List<InstructorExamProjection> batchExams = exams
                        .Where(x => x.BatchId == g.Key.BatchId)
                        .ToList();

                    List<InstructorExamStatusProjection> batchExamStatuses = new List<InstructorExamStatusProjection>();

                    foreach (InstructorExamProjection exam in batchExams)
                    {
                        batchExamStatuses.AddRange(
                            examStatuses.Where(x => x.ExamAssignmentId == exam.ExamAssignmentId).ToList()
                        );
                    }

                    List<InstructorExamStatusProjection> scoredRows = batchExamStatuses
                        .Where(x => x.Score.HasValue)
                        .ToList();

                    double batchExamAverage = scoredRows.Count > 0
                        ? Math.Round(scoredRows.Average(x => x.Score!.Value), 2)
                        : 0;

                    double healthScore = Math.Round(
                        (batchAttendancePercent * 0.45) +
                        (batchExamAverage * 0.55),
                        2);

                    return new InstructorBatchPerformanceVm
                    {
                        BatchId = g.Key.BatchId,
                        BatchName = g.Key.BatchName,
                        StudentsCount = batchStudentsCount,
                        LecturesCount = batchLectureCount,
                        AttendancePercent = batchAttendancePercent,
                        ExamAverageScore = batchExamAverage,
                        HealthScore = healthScore,
                        HealthCssClass = healthScore >= 75 ? "health-good" : healthScore >= 60 ? "health-warning" : "health-danger",
                        DecisionHint = healthScore >= 75 ? "الأداء مستقر" : healthScore >= 60 ? "تحتاج متابعة حضور ودرجات" : "تحتاج تدخل مباشر"
                    };
                })
                .OrderByDescending(x => x.HealthScore)
                .ToList();

            model.RecentLectures = lectures
                .Where(x => x.Date <= now)
                .OrderByDescending(x => x.Date)
                .Take(8)
                .Select(lecture => BuildInstructorLectureVm(lecture, attendanceRecords))
                .ToList();

            model.UpcomingLectures = lectures
                .Where(x => x.Date > now)
                .OrderBy(x => x.Date)
                .Take(8)
                .Select(lecture => BuildInstructorLectureVm(lecture, attendanceRecords))
                .ToList();

            model.Exams = exams
                .Take(8)
                .Select(exam =>
                {
                    List<InstructorExamStatusProjection> rows = examStatuses
                        .Where(x => x.ExamAssignmentId == exam.ExamAssignmentId)
                        .ToList();

                    int assigned = rows.Count;
                    int submitted = rows.Count(x => x.IsSubmitted);

                    List<InstructorExamStatusProjection> scoredRows = rows
                        .Where(x => x.Score.HasValue)
                        .ToList();

                    double average = scoredRows.Count > 0
                        ? Math.Round(scoredRows.Average(x => x.Score!.Value), 2)
                        : 0;

                    double submissionPercent = CalculateInstructorPercent(submitted, assigned);

                    return new InstructorExamImpactVm
                    {
                        ExamAssignmentId = exam.ExamAssignmentId,
                        Title = exam.Title,
                        BatchName = exam.BatchName,
                        CreatedAt = exam.CreatedAt,
                        ScheduledDate = exam.ScheduledDate,
                        AssignedCount = assigned,
                        SubmittedCount = submitted,
                        SubmissionPercent = submissionPercent,
                        AverageScore = average,
                        StatusText = submissionPercent >= 80 ? "مشاركة جيدة" : submissionPercent >= 50 ? "مشاركة متوسطة" : "مشاركة منخفضة",
                        StatusCssClass = submissionPercent >= 80 ? "good" : submissionPercent >= 50 ? "warning" : "danger"
                    };
                })
                .ToList();

            model.Homeworks = homeworks
                .Take(8)
                .Select(homework =>
                {
                    List<InstructorHomeworkStudentProjection> rows = homeworkStudents
                        .Where(x => x.HomeworkSetId == homework.HomeworkSetId)
                        .ToList();

                    int assigned = rows.Count;
                    int submitted = rows.Count(x => x.IsSubmitted);

                    List<InstructorHomeworkStudentProjection> scoredRows = rows
                        .Where(x => x.Score.HasValue)
                        .ToList();

                    double average = scoredRows.Count > 0
                        ? Math.Round(scoredRows.Average(x => x.Score!.Value), 2)
                        : 0;

                    double submissionPercent = CalculateInstructorPercent(submitted, assigned);

                    return new InstructorHomeworkImpactVm
                    {
                        HomeworkSetId = homework.HomeworkSetId,
                        Title = homework.Title,
                        BatchName = homework.BatchName,
                        CreatedAt = homework.CreatedAt,
                        EndAt = homework.EndAt,
                        AssignedCount = assigned,
                        SubmittedCount = submitted,
                        SubmissionPercent = submissionPercent,
                        AverageScore = average,
                        StatusText = submissionPercent >= 80 ? "تسليم جيد" : submissionPercent >= 50 ? "تسليم متوسط" : "تسليم منخفض",
                        StatusCssClass = submissionPercent >= 80 ? "good" : submissionPercent >= 50 ? "warning" : "danger"
                    };
                })
                .ToList();

            List<InstructorStudentWatchVm> studentPerformanceRows = students
                .Select(student =>
                {
                    int studentAttendanceTotal = attendanceRecords.Count(x => x.StudentId == student.StudentId);
                    int studentAttendancePresent = attendanceRecords.Count(x => x.StudentId == student.StudentId && x.IsPresent);
                    double studentAttendancePercent = CalculateInstructorPercent(studentAttendancePresent, studentAttendanceTotal);

                    List<InstructorExamStatusProjection> studentExamRows = examStatuses
                        .Where(x => x.StudentId == student.StudentId && x.Score.HasValue)
                        .ToList();

                    double studentExamAverage = studentExamRows.Count > 0
                        ? Math.Round(studentExamRows.Average(x => x.Score!.Value), 2)
                        : 0;

                    int studentHomeworkTotal = homeworkStudents.Count(x => x.StudentId == student.StudentId);
                    int studentHomeworkSubmitted = homeworkStudents.Count(x => x.StudentId == student.StudentId && x.IsSubmitted);
                    double studentHomeworkPercent = CalculateInstructorPercent(studentHomeworkSubmitted, studentHomeworkTotal);

                    double finalScore = Math.Round(
                        (studentAttendancePercent * 0.35) +
                        (studentExamAverage * 0.45) +
                        (studentHomeworkPercent * 0.20),
                        2);

                    List<string> flags = new List<string>();

                    if (studentAttendancePercent < 70)
                        flags.Add("حضور منخفض " + studentAttendancePercent.ToString("0") + "%");

                    if (studentExamAverage > 0 && studentExamAverage < 60)
                        flags.Add("درجات منخفضة " + studentExamAverage.ToString("0") + "%");

                    if (studentHomeworkPercent > 0 && studentHomeworkPercent < 70)
                        flags.Add("تسليم واجبات ضعيف");

                    return new InstructorStudentWatchVm
                    {
                        StudentId = student.StudentId,
                        FullName = student.FullName,
                        BatchName = student.BatchName,
                        AvatarText = BuildInstructorAvatar(student.FullName),
                        AttendancePercent = studentAttendancePercent,
                        ExamAverageScore = studentExamAverage,
                        HomeworkSubmissionPercent = studentHomeworkPercent,
                        FinalScore = finalScore,
                        TrendCssClass = finalScore >= 80 ? "up" : finalScore < 60 ? "dn" : "eq",
                        TrendText = finalScore >= 80 ? "↑ قوي" : finalScore < 60 ? "↓ خطر" : "→ مستقر",
                        Flags = flags
                    };
                })
                .ToList();

            model.TopStudents = studentPerformanceRows
                .OrderByDescending(x => x.FinalScore)
                .Take(5)
                .Select((x, index) =>
                {
                    x.Rank = index + 1;
                    return x;
                })
                .ToList();

            model.RiskStudents = studentPerformanceRows
                .OrderBy(x => x.FinalScore)
                .Take(5)
                .Select((x, index) =>
                {
                    x.Rank = index + 1;
                    return x;
                })
                .ToList();

            model.ProgressBars = new List<InstructorProgressBarVm>
    {
        new InstructorProgressBarVm
        {
            Label = "نشاط المدرب",
            Value = activityScore,
            Target = 80,
            CssColor = activityScore >= 80 ? "var(--green)" : activityScore >= 60 ? "var(--blue)" : "var(--red)"
        },
        new InstructorProgressBarVm
        {
            Label = "حضور الطلاب",
            Value = attendancePercent,
            Target = 85,
            CssColor = attendancePercent >= 85 ? "var(--green)" : attendancePercent >= 60 ? "var(--amber)" : "var(--red)"
        },
        new InstructorProgressBarVm
        {
            Label = "متوسط الاختبارات",
            Value = examAverageScore,
            Target = 80,
            CssColor = examAverageScore >= 80 ? "var(--green)" : examAverageScore >= 60 ? "var(--amber)" : "var(--red)"
        },
        new InstructorProgressBarVm
        {
            Label = "تسليم الواجبات",
            Value = homeworkSubmissionPercent,
            Target = 90,
            CssColor = homeworkSubmissionPercent >= 90 ? "var(--green)" : homeworkSubmissionPercent >= 60 ? "var(--amber)" : "var(--red)"
        },
        new InstructorProgressBarVm
        {
            Label = "تفاعل الطلاب",
            Value = participationPercent,
            Target = 85,
            CssColor = participationPercent >= 85 ? "var(--green)" : participationPercent >= 60 ? "var(--blue)" : "var(--red)"
        }
    };

            model.DiagnosisItems = BuildInstructorDiagnosis(model);
            model.HeatmapWeeks = BuildInstructorHeatmap(today, lectures, exams, homeworks);

            return View(model);
        }



        private static double CalculateInstructorPercent(int value, int total)
        {
            if (total <= 0)
                return 0;

            return Math.Round((value / (double)total) * 100, 2);
        }

        private static string BuildInstructorAvatar(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "مد";

            string cleaned = text.Trim();

            if (cleaned.Length == 1)
                return cleaned;

            string[] parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
                return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpper();

            return cleaned.Substring(0, Math.Min(2, cleaned.Length)).ToUpper();
        }

        private static string ResolveInstructorActivityLabel(double score)
        {
            if (score >= 85)
                return "ممتاز";

            if (score >= 70)
                return "نشط";

            if (score >= 55)
                return "يحتاج متابعة";

            return "منخفض";
        }

        private static string ResolveInstructorActivityCss(double score)
        {
            if (score >= 85)
                return "ex";

            if (score >= 70)
                return "gd";

            if (score >= 55)
                return "wn";

            return "dg";
        }

        private static string BuildInstructorDeltaText(double value, double target, string suffix)
        {
            double diff = Math.Round(value - target, 2);

            if (diff > 0)
                return "↑ +" + diff.ToString("0.##") + "% " + suffix;

            if (diff < 0)
                return "↓ " + diff.ToString("0.##") + "% " + suffix;

            return "مطابق للهدف";
        }

        private static InstructorLectureVm BuildInstructorLectureVm(
            InstructorLectureProjection lecture,
            List<InstructorAttendanceProjection> attendanceRecords)
        {
            List<InstructorAttendanceProjection> lectureAttendance = attendanceRecords
                .Where(x => x.LectureId == lecture.LectureId)
                .ToList();

            int total = lectureAttendance.Count;
            int present = lectureAttendance.Count(x => x.IsPresent);
            double percent = CalculateInstructorPercent(present, total);

            string statusText = "مجدولة";
            string statusCss = "scheduled";

            if (lecture.Date.Date < DateTime.Today)
            {
                statusText = percent >= 70 ? "مكتملة" : "حضور منخفض";
                statusCss = percent >= 70 ? "completed" : "danger";
            }
            else if (lecture.Date.Date == DateTime.Today)
            {
                statusText = "اليوم";
                statusCss = "today";
            }

            return new InstructorLectureVm
            {
                LectureId = lecture.LectureId,
                Title = lecture.Title,
                BatchName = lecture.BatchName,
                Date = lecture.Date,
                PresentCount = present,
                TotalCount = total,
                AttendancePercent = percent,
                StatusText = statusText,
                StatusCssClass = statusCss
            };
        }

        private static List<InstructorDiagnosisItemVm> BuildInstructorDiagnosis(InstructorDetailsDashboardViewModel model)
        {
            List<InstructorDiagnosisItemVm> result = new List<InstructorDiagnosisItemVm>();

            if (model.ActivityScore >= 80)
            {
                result.Add(new InstructorDiagnosisItemVm
                {
                    CssClass = "str",
                    Icon = "💪",
                    Title = "نقطة قوة — نشاط تدريسي مرتفع",
                    Body = "مؤشر نشاط المدرب " + model.ActivityScore.ToString("0.##") + "%، وهذا يعكس انتظامًا جيدًا في المحاضرات والمحتوى."
                });
            }

            if (model.AttendancePercent < 70)
            {
                result.Add(new InstructorDiagnosisItemVm
                {
                    CssClass = "risk",
                    Icon = "🚨",
                    Title = "خطر — حضور الطلاب منخفض",
                    Body = "متوسط حضور الطلاب في محاضرات هذا المدرب " + model.AttendancePercent.ToString("0.##") + "%، ويحتاج متابعة مباشرة."
                });
            }

            if (model.ExamAverageScore >= 80)
            {
                result.Add(new InstructorDiagnosisItemVm
                {
                    CssClass = "str",
                    Icon = "🏆",
                    Title = "قوة — نتائج اختبارات جيدة",
                    Body = "متوسط درجات الطلاب في اختبارات هذا المدرب " + model.ExamAverageScore.ToString("0.##") + "%."
                });
            }
            else if (model.ExamsCreatedCount > 0 && model.ExamAverageScore < 60)
            {
                result.Add(new InstructorDiagnosisItemVm
                {
                    CssClass = "wkn",
                    Icon = "⚠️",
                    Title = "ضعف — متوسط الاختبارات منخفض",
                    Body = "متوسط درجات الاختبارات أقل من 60%. يفضل مراجعة المحاور التي سبقت هذه الاختبارات."
                });
            }

            if (model.HomeworksCreatedCount > 0 && model.HomeworkSubmissionPercent < 70)
            {
                result.Add(new InstructorDiagnosisItemVm
                {
                    CssClass = "wkn",
                    Icon = "📝",
                    Title = "ضعف — تسليم واجبات منخفض",
                    Body = "نسبة تسليم الواجبات " + model.HomeworkSubmissionPercent.ToString("0.##") + "%، ويجب إرسال تذكير للطلاب المتأخرين."
                });
            }

            if (model.RiskStudents.Count > 0)
            {
                result.Add(new InstructorDiagnosisItemVm
                {
                    CssClass = "opp",
                    Icon = "🎯",
                    Title = "فرصة تدخل — طلاب يحتاجون متابعة",
                    Body = "يوجد " + model.RiskStudents.Count + " طلاب في نطاق الخطر داخل دفعات هذا المدرب."
                });
            }

            if (result.Count == 0)
            {
                result.Add(new InstructorDiagnosisItemVm
                {
                    CssClass = "str",
                    Icon = "✅",
                    Title = "حالة مستقرة",
                    Body = "لا توجد مؤشرات حرجة حالياً في ملف هذا المدرب."
                });
            }

            return result;
        }

        private static List<InstructorHeatmapWeekVm> BuildInstructorHeatmap(
            DateTime today,
            List<InstructorLectureProjection> lectures,
            List<InstructorExamProjection> exams,
            List<InstructorHomeworkProjection> homeworks)
        {
            List<InstructorHeatmapWeekVm> result = new List<InstructorHeatmapWeekVm>();

            DateTime start = today.Date.AddDays(-20);

            for (int week = 0; week < 3; week++)
            {
                InstructorHeatmapWeekVm weekVm = new InstructorHeatmapWeekVm();

                for (int day = 0; day < 7; day++)
                {
                    DateTime current = start.AddDays((week * 7) + day);

                    int lecturesCount = lectures.Count(x => x.Date.Date == current.Date);
                    int examsCount = exams.Count(x => x.CreatedAt.Date == current.Date);
                    int homeworksCount = homeworks.Count(x => x.CreatedAt.Date == current.Date);

                    int total = lecturesCount + examsCount + homeworksCount;

                    string style = "background:var(--bg-2);color:var(--mute)";

                    if (total >= 8)
                        style = "background:#1d4ed8;color:#fff";
                    else if (total >= 5)
                        style = "background:#3b82f6;color:#fff";
                    else if (total >= 3)
                        style = "background:#93c5fd;color:#1e3a8a";
                    else if (total > 0)
                        style = "background:#dbeafe;color:#1e3a8a";

                    weekVm.Days.Add(new InstructorHeatmapDayVm
                    {
                        ActivityCount = total,
                        CssStyle = style
                    });
                }

                result.Add(weekVm);
            }

            return result;
        }




        // GET: Admin/Instructors/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var instructor = await _context.Instructors.FindAsync(id);
            if (instructor == null) return NotFound();

            var vm = new InstructorViewModel
            {
                Id = instructor.Id,
                FullName = instructor.FullName,
                Email = instructor.Email,
                PhoneNumber = instructor.PhoneNumber,
                WhatsAppNumber = instructor.WhatsAppNumber,
                Gender = instructor.Gender,
                Specialization = instructor.Specialization,
                IsActive = instructor.IsActive
            };

            return View(vm);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var instructor = await _context.Instructors.FindAsync(id);

            if (instructor == null)
            {
                return NotFound();
            }

            _context.Instructors.Remove(instructor);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف المدرب بنجاح.";
            return RedirectToAction(nameof(Index));
        }


        private class InstructorLectureProjection
        {
            public int LectureId { get; set; }

            public string Title { get; set; } = string.Empty;

            public DateTime Date { get; set; }

            public int BatchId { get; set; }

            public string BatchName { get; set; } = string.Empty;
        }

        private class InstructorStudentProjection
        {
            public int StudentId { get; set; }

            public string FullName { get; set; } = string.Empty;

            public int BatchId { get; set; }

            public string BatchName { get; set; } = string.Empty;
        }

        private class InstructorAttendanceProjection
        {
            public int LectureId { get; set; }

            public int StudentId { get; set; }

            public bool IsPresent { get; set; }

            public DateTime RecordedAt { get; set; }
        }

        private class InstructorExamProjection
        {
            public int ExamAssignmentId { get; set; }

            public string Title { get; set; } = string.Empty;

            public int BatchId { get; set; }

            public string BatchName { get; set; } = string.Empty;

            public DateTime CreatedAt { get; set; }

            public DateTime? ScheduledDate { get; set; }
        }

        private class InstructorExamStatusProjection
        {
            public int ExamAssignmentId { get; set; }

            public int StudentId { get; set; }

            public bool IsSubmitted { get; set; }

            public int? Score { get; set; }
        }

        private class InstructorHomeworkProjection
        {
            public int HomeworkSetId { get; set; }

            public string Title { get; set; } = string.Empty;

            public int BatchId { get; set; }

            public string BatchName { get; set; } = string.Empty;

            public DateTime CreatedAt { get; set; }

            public DateTime? EndAt { get; set; }
        }

        private class InstructorHomeworkStudentProjection
        {
            public int HomeworkSetId { get; set; }

            public int StudentId { get; set; }

            public bool IsSubmitted { get; set; }

            public double? Score { get; set; }
        }



    }
}
