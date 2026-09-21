using EFCore.BulkExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Students;
using StudentViewModel = QdratNew.ViewModels.StudentViewModel;
using BatchCardVm = QdratNew.ViewModels.Students.BatchCardVm;
using BatchIndexVm = QdratNew.ViewModels.Students.BatchIndexVm;
using BatchStudentsVm = QdratNew.ViewModels.Students.BatchStudentsVm;
using StudentInBatchVm = QdratNew.ViewModels.Students.StudentInBatchVm;
using BatchBreakdownRowVm = QdratNew.ViewModels.Students.BatchBreakdownRowVm;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class StudentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHomeworkAssignmentService _homeworkAssignmentService;
        private readonly IDropdownService _dropdownService;

        public StudentsController(ApplicationDbContext context, IDropdownService dropdownService, IHomeworkAssignmentService homeworkAssignmentService)
        {
            _context = context;
            _dropdownService = dropdownService;
            _homeworkAssignmentService = homeworkAssignmentService;

        }

        public async Task<IActionResult> Dashboard(StudentFilterViewModel filter)
        {
            filter.Branches = await _dropdownService.GetBranchesAsync();
            filter.Batches = await _dropdownService.GetBatchesAsync();

            int selectedBranchId = 0;
            int selectedBatchId = 0;

            bool hasBranchFilter = !string.IsNullOrWhiteSpace(filter.SelectedBranchId)
                                   && int.TryParse(filter.SelectedBranchId, out selectedBranchId);

            bool hasBatchFilter = !string.IsNullOrWhiteSpace(filter.SelectedBatchId)
                                  && int.TryParse(filter.SelectedBatchId, out selectedBatchId);

            var query = _context.Students
                .AsNoTracking()
                .AsQueryable();

            if (hasBranchFilter)
            {
                query = query.Where(s => s.BranchId == selectedBranchId);
            }

            if (!string.IsNullOrWhiteSpace(filter.Gender))
            {
                query = query.Where(s => s.Gender == filter.Gender);
            }

            if (!string.IsNullOrWhiteSpace(filter.Level))
            {
                query = query.Where(s => s.Level == filter.Level);
            }

            if (!string.IsNullOrWhiteSpace(filter.EnrollmentStatus))
            {
                query = query.Where(s => s.EnrollmentStatus == filter.EnrollmentStatus);
            }

            if (hasBatchFilter)
            {
                query = query.Where(s =>
                    _context.StudentBatchEnrollments.Any(e =>
                        e.StudentID == s.StudentID &&
                        e.BatchId == selectedBatchId));
            }

            filter.Students = await query
                .OrderBy(s => s.FullName)
                .Select(s => new StudentViewModel
                {
                    StudentID = s.StudentID,
                    FullName = s.FullName,
                    Gender = s.Gender,
                    Level = s.Level,
                    PhoneNumber = s.PhoneNumber,
                    WhatsAppNumber = s.WhatsAppNumber,
                    Email = s.Email,
                    EnrollmentStatus = s.EnrollmentStatus,
                    BranchId = s.BranchId,
                    BatchId = hasBatchFilter ? selectedBatchId : 0
                })
                .ToListAsync();

            return View(filter);
        }


        public async Task<IActionResult> Index()
        {
            var batches = await (
                from b in _context.Batches.AsNoTracking()
                where !b.IsDeleted && !b.IsArchived
                join c in _context.Courses.AsNoTracking() on b.CourseId equals c.Id into cj
                from c in cj.DefaultIfEmpty()
                join br in _context.Branches.AsNoTracking() on b.BranchId equals br.Id into brj
                from br in brj.DefaultIfEmpty()
                orderby b.StartDate descending
                select new BatchCardVm
                {
                    BatchId = b.Id,
                    BatchName = b.Name,
                    CourseName = c != null ? c.Name : "—",
                    BranchName = br != null ? br.Name : "—",
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    IsActive = b.IsActive,
                    GenderLabel = b.Gender == BatchGender.ذكور ? "ذكور"
                                : b.Gender == BatchGender.إناث ? "إناث" : "مختلط",
                    StudentCount = _context.StudentBatchEnrollments.Count(e => e.BatchId == b.Id)
                }).ToListAsync();

            int totalStudents = await _context.Students.AsNoTracking().CountAsync();
            int totalCourses  = batches.Select(b => b.CourseName).Distinct().Count();

            var vm = new BatchIndexVm
            {
                Batches = batches,
                TotalStudents = totalStudents,
                TotalBatches = batches.Count,
                ActiveBatches = batches.Count(b => b.IsActive),
                TotalCourses = totalCourses
            };

            return View(vm);
        }

        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee")]
        public async Task<IActionResult> BatchStudents(int id)
        {
            var batch = await (
                from b in _context.Batches.AsNoTracking()
                join c in _context.Courses.AsNoTracking() on b.CourseId equals c.Id into cj
                from c in cj.DefaultIfEmpty()
                join br in _context.Branches.AsNoTracking() on b.BranchId equals br.Id into brj
                from br in brj.DefaultIfEmpty()
                where b.Id == id
                select new { b.Id, b.Name, b.StartDate, b.EndDate, CourseName = c != null ? c.Name : "—", BranchName = br != null ? br.Name : "—" }
            ).FirstOrDefaultAsync();

            if (batch == null) return NotFound();

            // إجمالي محاضرات الدفعة
            var batchLectures = await _context.Lecture.AsNoTracking()
                .Where(l => l.BatchId == id)
                .Select(l => l.Id)
                .ToListAsync();

            int totalLectures = batchLectures.Count;

            // الطلاب المسجلون في الدفعة
            var enrollments = await _context.StudentBatchEnrollments.AsNoTracking()
                .Where(e => e.BatchId == id)
                .Select(e => new { e.StudentID, e.EnrolledAt })
                .ToListAsync();

            var studentIds = enrollments.Select(e => e.StudentID).ToList();

            var studentsData = await _context.Students.AsNoTracking()
                .Where(s => studentIds.Contains(s.StudentID))
                .Select(s => new
                {
                    s.StudentID,
                    s.FullName,
                    s.PhoneNumber,
                    s.WhatsAppNumber,
                    s.Gender,
                    s.Level,
                    s.EnrollmentStatus
                }).ToListAsync();

            // حضور الدفعة
            var attendanceData = await _context.AttendanceRecords.AsNoTracking()
                .Where(a => batchLectures.Contains(a.LectureId))
                .Select(a => new { a.StudentId, a.IsPresent })
                .ToListAsync();

            // واجبات الدفعة
            var hwSets = await _context.HomeworkSets.AsNoTracking()
                .Where(hs => hs.BatchId == id)
                .Select(hs => hs.Id)
                .ToListAsync();

            var hwData = await _context.HomeworkSetStudents.AsNoTracking()
                .Where(h => hwSets.Contains(h.HomeworkSetId) && studentIds.Contains(h.StudentId))
                .Select(h => new { h.StudentId, h.IsSubmitted })
                .ToListAsync();

            // اختبارات الدفعة
            var examAssignments = await _context.ExamAssignmentsToBatches.AsNoTracking()
                .Where(a => a.BatchId == id)
                .Select(a => a.Id)
                .ToListAsync();

            var examData = await _context.ExamStudentStatuses.AsNoTracking()
                .Where(s => examAssignments.Contains(s.ExamAssignmentId ?? 0) && studentIds.Contains(s.StudentId))
                .Select(s => new { s.StudentId, s.IsSubmitted })
                .ToListAsync();

            var enrollmentMap = enrollments.ToDictionary(e => e.StudentID, e => e.EnrolledAt);

            var students = studentsData.Select(s => new StudentInBatchVm
            {
                StudentId = s.StudentID,
                FullName = s.FullName ?? "بدون اسم",
                PhoneNumber = s.PhoneNumber,
                WhatsAppNumber = s.WhatsAppNumber,
                Gender = s.Gender,
                Level = s.Level,
                EnrollmentStatus = s.EnrollmentStatus,
                EnrolledAt = enrollmentMap.TryGetValue(s.StudentID, out var ea) ? ea : DateTime.MinValue,
                TotalLectures = totalLectures,
                AttendedLectures = attendanceData.Count(a => a.StudentId == s.StudentID && a.IsPresent),
                TotalHomeworks = hwData.Count(h => h.StudentId == s.StudentID),
                SubmittedHomeworks = hwData.Count(h => h.StudentId == s.StudentID && h.IsSubmitted),
                TotalExams = examData.Count(e => e.StudentId == s.StudentID),
                SubmittedExams = examData.Count(e => e.StudentId == s.StudentID && e.IsSubmitted)
            }).OrderBy(s => s.FullName).ToList();

            var vm = new BatchStudentsVm
            {
                BatchId = batch.Id,
                BatchName = batch.Name,
                CourseName = batch.CourseName,
                BranchName = batch.BranchName,
                StartDate = batch.StartDate,
                EndDate = batch.EndDate,
                Students = students
            };

            return View(vm);
        }


        public async Task<IActionResult> Details(int id)
        {
            DateTime today = DateTime.Today;
            DateTime now = DateTime.Now;
            DateTime monthStart = new DateTime(today.Year, today.Month, 1);
            DateTime nextMonthStart = monthStart.AddMonths(1);

            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.Branch)
                .Include(s => s.Parent)
                .FirstOrDefaultAsync(s => s.StudentID == id);

            if (student == null)
                return NotFound();

            List<StudentBatchProjection> batches = await (
                from enrollment in _context.StudentBatchEnrollments.AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on enrollment.BatchId equals batch.Id
                join course in _context.Courses.AsNoTracking()
                    on batch.CourseId equals course.Id
                join branch in _context.Branches.AsNoTracking()
                    on batch.BranchId equals branch.Id
                where enrollment.StudentID == id
                select new StudentBatchProjection
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    CourseName = course.Name,
                    BranchName = branch.Name,
                    EnrolledAt = enrollment.EnrolledAt,
                    Status = enrollment.Status
                })
                .ToListAsync();

            List<StudentAttendanceProjection> attendanceRows = await (
                from attendance in _context.AttendanceRecords.AsNoTracking()
                join lecture in _context.Lecture.AsNoTracking()
                    on attendance.LectureId equals lecture.Id
                join batch in _context.Batches.AsNoTracking()
                    on lecture.BatchId equals batch.Id
                where attendance.StudentId == id
                select new StudentAttendanceProjection
                {
                    LectureId = lecture.Id,
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    LectureDate = lecture.Date,
                    IsPresent = attendance.IsPresent,
                    RecordedAt = attendance.RecordedAt
                })
                .ToListAsync();

            List<StudentHomeworkProjection> homeworkRows = await (
                from homeworkStudent in _context.HomeworkSetStudents.AsNoTracking()
                join homeworkSet in _context.HomeworkSets.AsNoTracking()
                    on homeworkStudent.HomeworkSetId equals homeworkSet.Id
                join batch in _context.Batches.AsNoTracking()
                    on homeworkSet.BatchId equals batch.Id
                where homeworkStudent.StudentId == id
                select new StudentHomeworkProjection
                {
                    HomeworkSetId = homeworkSet.Id,
                    Title = homeworkSet.Title,
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    AssignedAt = homeworkStudent.AssignedAt,
                    SubmittedAt = homeworkStudent.SubmittedAt,
                    EndAt = homeworkSet.EndAt,
                    IsSubmitted = homeworkStudent.IsSubmitted,
                    Score = homeworkStudent.Score
                })
                .OrderByDescending(x => x.AssignedAt)
                .ToListAsync();

            List<StudentExamProjection> examRows = await (
                from status in _context.ExamStudentStatuses.AsNoTracking()
                join assignment in _context.ExamAssignmentsToBatches.AsNoTracking()
                    on status.ExamAssignmentId equals (int?)assignment.Id
                join batch in _context.Batches.AsNoTracking()
                    on assignment.BatchId equals batch.Id
                where status.StudentId == id
                select new StudentExamProjection
                {
                    StatusId = status.Id,
                    ExamAssignmentId = status.ExamAssignmentId,
                    Title = assignment.Title,
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    AssignedAt = status.AssignedAt,
                    StartedAt = status.StartedAt,
                    SubmittedAt = status.SubmittedAt,
                    IsSubmitted = status.IsSubmitted,
                    Score = status.Score
                })
                .OrderByDescending(x => x.AssignedAt)
                .ToListAsync();

            List<StudentPerformanceProjection> performanceRows = await _context.StudentPerformances
                .AsNoTracking()
                .Where(x => x.StudentID == id)
                .Select(x => new StudentPerformanceProjection
                {
                    Id = x.Id,
                    ExamDate = x.ExamDate,
                    Score = x.Score,
                    WeakTopics = x.WeakTopics,
                    EngagementScore = x.EngagementScore
                })
                .OrderByDescending(x => x.ExamDate)
                .ToListAsync();

            List<StudentQuestionAttemptProjection> questionAttempts = await _context.QuestionAttemptNew
         .AsNoTracking()
         .Where(x => x.StudentId == id)
         .Select(x => new StudentQuestionAttemptProjection
         {
             Id = x.Id,
             AttemptedAt = x.AttemptedAt,
             IsCorrect = x.IsCorrect,
             SectionId = x.SectionId,
             LessonId = x.LessonId,
             TimeTakenSeconds = x.TimeTakenSeconds,
             IsMarkedForReview = x.IsMarkedForReview.HasValue && x.IsMarkedForReview.Value
         })
         .OrderByDescending(x => x.AttemptedAt)
         .ToListAsync();

            List<StudentRemedialPlanProjection> remedialPlans = await _context.RemedialPlans
           .AsNoTracking()
           .Where(x => x.StudentID == id)
           .Select(x => new StudentRemedialPlanProjection
           {
               Id = x.Id,
               Title = x.Title,
               PerformanceLevel = x.PerformanceLevel,
               CreatedAt = x.CreatedAt,
               TotalLessons = x.TotalLessons.HasValue ? x.TotalLessons.Value : 0,
               CompletedLessons = x.CompletedLessons.HasValue ? x.CompletedLessons.Value : 0,
               IsCompleted = x.IsCompleted.HasValue && x.IsCompleted.Value
           })
           .OrderByDescending(x => x.CreatedAt)
           .ToListAsync();

            List<StudentProgressProjection> progressRows = await _context.Students
         .AsNoTracking()
         .Where(s => s.StudentID == id)
         .SelectMany(s => s.StudentProgressRecords)
         .Select(x => new StudentProgressProjection
         {
             Id = x.Id,
             Date = x.Date,
             Topic = x.Topic,
             ProgressPercentage = x.ProgressPercentage,
             Score = x.Score
         })
         .OrderByDescending(x => x.Date)
         .ToListAsync();

            int attendanceTotal = attendanceRows.Count;
            int attendancePresent = attendanceRows.Count(x => x.IsPresent);
            double attendancePercent = CalculateStudentPercent(attendancePresent, attendanceTotal);

            int homeworkTotal = homeworkRows.Count;
            int homeworkSubmitted = homeworkRows.Count(x => x.IsSubmitted);
            int homeworkMissing = homeworkTotal - homeworkSubmitted;
            double homeworkSubmissionPercent = CalculateStudentPercent(homeworkSubmitted, homeworkTotal);

            List<StudentExamProjection> submittedExamRows = examRows
                .Where(x => x.IsSubmitted)
                .ToList();

            List<StudentExamProjection> scoredExamRows = examRows
                .Where(x => x.Score.HasValue)
                .ToList();

            double examAverageScore = scoredExamRows.Count > 0
                ? Math.Round(scoredExamRows.Average(x => x.Score!.Value), 2)
                : 0;

            int questionAttemptsCount = questionAttempts.Count;
            int correctQuestionAttemptsCount = questionAttempts.Count(x => x.IsCorrect);
            double correctAnswerPercent = CalculateStudentPercent(correctQuestionAttemptsCount, questionAttemptsCount);

            double studyProgressPercent = progressRows.Count > 0
                ? Math.Round(progressRows.Average(x => x.ProgressPercentage), 2)
                : 0;

            double learningHealthScore = Math.Round(
                (attendancePercent * 0.25) +
                (homeworkSubmissionPercent * 0.25) +
                (examAverageScore * 0.35) +
                (correctAnswerPercent * 0.15),
                2);

            StudentDetailsDashboardViewModel model = new StudentDetailsDashboardViewModel
            {
                StudentID = student.StudentID,
                FullName = student.FullName,
                NationalID = student.NationalID,
                Email = student.Email,
                PhoneNumber = student.PhoneNumber,
                WhatsAppNumber = student.WhatsAppNumber,
                Gender = student.Gender,
                School = student.School,
                Level = student.Level,
                EnrollmentStatus = student.EnrollmentStatus,
                BranchName = student.Branch != null ? student.Branch.Name : "غير محدد",
                ParentName = student.Parent != null ? student.Parent.FullName : "غير محدد",
                RegistrationDate = student.RegistrationDate,
                LastLoginAt = student.LastLoginAt,
                AvatarText = BuildStudentAvatar(student.FullName),
                StatusText = string.IsNullOrWhiteSpace(student.EnrollmentStatus) ? "غير محدد" : student.EnrollmentStatus,
                LearningHealthScore = learningHealthScore,
                LearningHealthLabel = ResolveStudentHealthLabel(learningHealthScore),
                LearningHealthCssClass = ResolveStudentHealthCss(learningHealthScore),
                BatchesCount = batches.Count,
                AttendanceRecordsCount = attendanceRows.Count,
                AttendancePercent = attendancePercent,
                HomeworkSubmissionPercent = homeworkSubmissionPercent,
                ExamAverageScore = examAverageScore,
                CorrectAnswerPercent = correctAnswerPercent,
                HomeworksAssignedCount = homeworkTotal,
                HomeworksSubmittedCount = homeworkSubmitted,
                HomeworksMissingCount = homeworkMissing,
                ExamsAssignedCount = examRows.Count,
                ExamsSubmittedCount = submittedExamRows.Count,
                QuestionAttemptsCount = questionAttemptsCount,
                CorrectQuestionAttemptsCount = correctQuestionAttemptsCount,
                RemedialPlansCount = remedialPlans.Count,
                CompletedRemedialPlansCount = remedialPlans.Count(x => x.IsCompleted),
                StudyProgressPercent = studyProgressPercent,
                AttendanceDeltaText = BuildStudentDeltaText(attendancePercent, 85, "عن هدف الحضور"),
                HomeworkDeltaText = BuildStudentDeltaText(homeworkSubmissionPercent, 90, "عن هدف الواجبات"),
                ExamDeltaText = BuildStudentDeltaText(examAverageScore, 80, "عن هدف الاختبارات"),
                CorrectAnswerDeltaText = BuildStudentDeltaText(correctAnswerPercent, 75, "عن هدف الإجابات الصحيحة")
            };

            model.Batches = batches
                .Select(batch =>
                {
                    List<StudentAttendanceProjection> batchAttendance = attendanceRows
                        .Where(x => x.BatchId == batch.BatchId)
                        .ToList();

                    List<StudentHomeworkProjection> batchHomeworks = homeworkRows
                        .Where(x => x.BatchId == batch.BatchId)
                        .ToList();

                    List<StudentExamProjection> batchExams = examRows
                        .Where(x => x.BatchId == batch.BatchId)
                        .ToList();

                    double batchAttendancePercent = CalculateStudentPercent(
                        batchAttendance.Count(x => x.IsPresent),
                        batchAttendance.Count);

                    double batchHomeworkPercent = CalculateStudentPercent(
                        batchHomeworks.Count(x => x.IsSubmitted),
                        batchHomeworks.Count);

                    List<StudentExamProjection> batchScoredExams = batchExams
                        .Where(x => x.Score.HasValue)
                        .ToList();

                    double batchExamAverage = batchScoredExams.Count > 0
                        ? Math.Round(batchScoredExams.Average(x => x.Score!.Value), 2)
                        : 0;

                    double batchHealth = Math.Round(
                        (batchAttendancePercent * 0.30) +
                        (batchHomeworkPercent * 0.30) +
                        (batchExamAverage * 0.40),
                        2);

                    return new StudentBatchDetailsVm
                    {
                        BatchId = batch.BatchId,
                        BatchName = batch.BatchName,
                        CourseName = batch.CourseName,
                        BranchName = batch.BranchName,
                        EnrolledAt = batch.EnrolledAt,
                        Status = batch.Status,
                        AttendancePercent = batchAttendancePercent,
                        HomeworkSubmissionPercent = batchHomeworkPercent,
                        ExamAverageScore = batchExamAverage,
                        BatchStudentHealthScore = batchHealth,
                        HealthCssClass = batchHealth >= 75 ? "health-good" : batchHealth >= 60 ? "health-warning" : "health-danger",
                        DecisionHint = batchHealth >= 75 ? "أداء مستقر داخل الدفعة" : batchHealth >= 60 ? "يحتاج متابعة داخل الدفعة" : "يحتاج تدخل مباشر"
                    };
                })
                .OrderByDescending(x => x.BatchStudentHealthScore)
                .ToList();

            model.AttendanceCalendar = BuildStudentAttendanceCalendar(monthStart, nextMonthStart, attendanceRows, today);

            model.Homeworks = homeworkRows
                .Take(10)
                .Select(homework =>
                {
                    string statusText = "غير محلول";
                    string statusCss = "warning";

                    if (homework.IsSubmitted)
                    {
                        statusText = "تم التسليم";
                        statusCss = "good";
                    }
                    else if (homework.EndAt.HasValue && homework.EndAt.Value < now)
                    {
                        statusText = "متأخر";
                        statusCss = "danger";
                    }
                    else if (homework.EndAt.HasValue && homework.EndAt.Value <= now.AddDays(2))
                    {
                        statusText = "يغلق قريبًا";
                        statusCss = "soon";
                    }

                    return new StudentHomeworkDetailsVm
                    {
                        HomeworkSetId = homework.HomeworkSetId,
                        Title = homework.Title,
                        BatchName = homework.BatchName,
                        AssignedAt = homework.AssignedAt,
                        SubmittedAt = homework.SubmittedAt,
                        EndAt = homework.EndAt,
                        IsSubmitted = homework.IsSubmitted,
                        Score = homework.Score,
                        StatusText = statusText,
                        StatusCssClass = statusCss
                    };
                })
                .ToList();

            model.Exams = examRows
                .Take(10)
                .Select(exam =>
                {
                    string statusText = "لم يبدأ";
                    string statusCss = "warning";

                    if (exam.IsSubmitted)
                    {
                        statusText = "تم التسليم";
                        statusCss = "good";
                    }
                    else if (exam.StartedAt.HasValue)
                    {
                        statusText = "بدأ ولم ينته";
                        statusCss = "soon";
                    }

                    if (exam.Score.HasValue && exam.Score.Value < 60)
                    {
                        statusText = "درجة منخفضة";
                        statusCss = "danger";
                    }

                    return new StudentExamDetailsVm
                    {
                        ExamStudentStatusId = exam.StatusId,
                        ExamAssignmentId = exam.ExamAssignmentId,
                        Title = exam.Title,
                        BatchName = exam.BatchName,
                        AssignedAt = exam.AssignedAt,
                        StartedAt = exam.StartedAt,
                        SubmittedAt = exam.SubmittedAt,
                        IsSubmitted = exam.IsSubmitted,
                        Score = exam.Score,
                        StatusText = statusText,
                        StatusCssClass = statusCss
                    };
                })
                .ToList();

            model.Performances = performanceRows
                .Take(8)
                .Select(row => new StudentPerformanceDetailsVm
                {
                    Id = row.Id,
                    ExamDate = row.ExamDate,
                    Score = row.Score,
                    Level = ResolveStudentPerformanceLevel(row.Score),
                    WeakTopics = row.WeakTopics,
                    EngagementScore = row.EngagementScore,
                    CssClass = row.Score >= 80 ? "good" : row.Score >= 60 ? "warning" : "danger"
                })
                .ToList();

            model.RemedialPlans = remedialPlans
                .Take(8)
                .Select(plan =>
                {
                    double completionPercent = CalculateStudentPercent(plan.CompletedLessons, plan.TotalLessons);

                    return new StudentRemedialPlanDetailsVm
                    {
                        Id = plan.Id,
                        Title = plan.Title,
                        PerformanceLevel = plan.PerformanceLevel ?? "غير محدد",
                        CreatedAt = plan.CreatedAt,
                        TotalLessons = plan.TotalLessons,
                        CompletedLessons = plan.CompletedLessons,
                        CompletionPercent = completionPercent,
                        IsCompleted = plan.IsCompleted,
                        StatusText = plan.IsCompleted ? "مكتملة" : "قيد التنفيذ",
                        StatusCssClass = plan.IsCompleted ? "good" : "warning"
                    };
                })
                .ToList();

            model.WeaknessSections = questionAttempts
                .GroupBy(x => x.SectionId)
                .Select(g =>
                {
                    int total = g.Count();
                    int wrong = g.Count(x => !x.IsCorrect);
                    double wrongPercent = CalculateStudentPercent(wrong, total);

                    return new StudentWeaknessSectionVm
                    {
                        SectionId = g.Key,
                        Title = g.Key.HasValue ? "محور رقم " + g.Key.Value : "محور غير محدد",
                        WrongAttemptsCount = wrong,
                        TotalAttemptsCount = total,
                        WrongPercent = wrongPercent,
                        CssClass = wrongPercent >= 50 ? "danger" : wrongPercent >= 30 ? "warning" : "good"
                    };
                })
                .OrderByDescending(x => x.WrongPercent)
                .Take(6)
                .ToList();

            model.ProgressBars = new List<StudentProgressBarVm>
    {
        new StudentProgressBarVm
        {
            Label = "صحة الطالب التعليمية",
            Value = learningHealthScore,
            Target = 80,
            CssColor = learningHealthScore >= 80 ? "var(--green)" : learningHealthScore >= 60 ? "var(--blue)" : "var(--red)"
        },
        new StudentProgressBarVm
        {
            Label = "الحضور",
            Value = attendancePercent,
            Target = 85,
            CssColor = attendancePercent >= 85 ? "var(--green)" : attendancePercent >= 60 ? "var(--amber)" : "var(--red)"
        },
        new StudentProgressBarVm
        {
            Label = "تسليم الواجبات",
            Value = homeworkSubmissionPercent,
            Target = 90,
            CssColor = homeworkSubmissionPercent >= 90 ? "var(--green)" : homeworkSubmissionPercent >= 60 ? "var(--amber)" : "var(--red)"
        },
        new StudentProgressBarVm
        {
            Label = "متوسط الاختبارات",
            Value = examAverageScore,
            Target = 80,
            CssColor = examAverageScore >= 80 ? "var(--green)" : examAverageScore >= 60 ? "var(--amber)" : "var(--red)"
        },
        new StudentProgressBarVm
        {
            Label = "الإجابات الصحيحة",
            Value = correctAnswerPercent,
            Target = 75,
            CssColor = correctAnswerPercent >= 75 ? "var(--green)" : correctAnswerPercent >= 55 ? "var(--amber)" : "var(--red)"
        },
        new StudentProgressBarVm
        {
            Label = "التقدم الدراسي",
            Value = studyProgressPercent,
            Target = 80,
            CssColor = studyProgressPercent >= 80 ? "var(--green)" : studyProgressPercent >= 60 ? "var(--blue)" : "var(--amber)"
        }
    };

            model.DiagnosisItems = BuildStudentDiagnosis(model);
            model.HeatmapWeeks = BuildStudentHeatmap(today, attendanceRows, homeworkRows, examRows, questionAttempts);

            return View(model);
        }


        private static double CalculateStudentPercent(int value, int total)
        {
            if (total <= 0)
                return 0;

            return Math.Round((value / (double)total) * 100, 2);
        }

        private static string BuildStudentAvatar(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "ط";

            string cleaned = text.Trim();

            if (cleaned.Length == 1)
                return cleaned;

            string[] parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
                return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpper();

            return cleaned.Substring(0, Math.Min(2, cleaned.Length)).ToUpper();
        }

        private static string ResolveStudentHealthLabel(double score)
        {
            if (score >= 85)
                return "ممتاز";

            if (score >= 70)
                return "مستقر";

            if (score >= 55)
                return "يحتاج متابعة";

            return "خطر";
        }

        private static string ResolveStudentHealthCss(double score)
        {
            if (score >= 85)
                return "ex";

            if (score >= 70)
                return "gd";

            if (score >= 55)
                return "wn";

            return "dg";
        }

        private static string BuildStudentDeltaText(double value, double target, string suffix)
        {
            double diff = Math.Round(value - target, 2);

            if (diff > 0)
                return "↑ +" + diff.ToString("0.##") + "% " + suffix;

            if (diff < 0)
                return "↓ " + diff.ToString("0.##") + "% " + suffix;

            return "مطابق للهدف";
        }

        private static string ResolveStudentPerformanceLevel(double score)
        {
            if (score >= 90)
                return "ممتاز";

            if (score >= 75)
                return "جيد جدًا";

            if (score >= 60)
                return "جيد";

            if (score >= 50)
                return "مقبول";

            return "ضعيف";
        }

        private static List<StudentAttendanceDayVm> BuildStudentAttendanceCalendar(
            DateTime monthStart,
            DateTime nextMonthStart,
            List<StudentAttendanceProjection> attendanceRows,
            DateTime today)
        {
            List<StudentAttendanceDayVm> result = new List<StudentAttendanceDayVm>();

            DateTime cursor = monthStart;

            while (cursor < nextMonthStart)
            {
                List<StudentAttendanceProjection> dayRows = attendanceRows
                    .Where(x => x.LectureDate.Date == cursor.Date || x.RecordedAt.Date == cursor.Date)
                    .ToList();

                bool hasLecture = dayRows.Count > 0;
                bool isPresent = dayRows.Any(x => x.IsPresent);

                string cssClass = "no-lecture";

                if (hasLecture && isPresent)
                    cssClass = "present";

                if (hasLecture && !isPresent)
                    cssClass = "absent";

                if (cursor.Date == today.Date)
                    cssClass += " today";

                result.Add(new StudentAttendanceDayVm
                {
                    Date = cursor,
                    DayText = cursor.Day.ToString(),
                    HasLecture = hasLecture,
                    IsPresent = isPresent,
                    IsToday = cursor.Date == today.Date,
                    CssClass = cssClass
                });

                cursor = cursor.AddDays(1);
            }

            return result;
        }

        private static List<StudentDiagnosisItemVm> BuildStudentDiagnosis(StudentDetailsDashboardViewModel model)
        {
            List<StudentDiagnosisItemVm> result = new List<StudentDiagnosisItemVm>();

            if (model.LearningHealthScore >= 80)
            {
                result.Add(new StudentDiagnosisItemVm
                {
                    CssClass = "str",
                    Icon = "💪",
                    Title = "نقطة قوة — صحة تعليمية جيدة",
                    Body = "مؤشر صحة الطالب " + model.LearningHealthScore.ToString("0.##") + "%، وهذا يعكس أداءً عامًا مستقرًا."
                });
            }

            if (model.AttendancePercent < 70)
            {
                result.Add(new StudentDiagnosisItemVm
                {
                    CssClass = "risk",
                    Icon = "🚨",
                    Title = "خطر — حضور منخفض",
                    Body = "نسبة حضور الطالب " + model.AttendancePercent.ToString("0.##") + "%، ويجب متابعة الغياب قبل تأثيره على المستوى."
                });
            }

            if (model.HomeworksMissingCount > 0)
            {
                result.Add(new StudentDiagnosisItemVm
                {
                    CssClass = "wkn",
                    Icon = "📝",
                    Title = "ضعف — واجبات متراكمة",
                    Body = "يوجد " + model.HomeworksMissingCount + " واجب لم يتم تسليمه، ويجب إرسال تذكير أو تدخل مباشر."
                });
            }

            if (model.ExamsAssignedCount > 0 && model.ExamAverageScore < 60)
            {
                result.Add(new StudentDiagnosisItemVm
                {
                    CssClass = "wkn",
                    Icon = "🧪",
                    Title = "ضعف — متوسط اختبارات منخفض",
                    Body = "متوسط اختبارات الطالب " + model.ExamAverageScore.ToString("0.##") + "%، ويحتاج مراجعة المحاور الضعيفة."
                });
            }

            if (model.CorrectAnswerPercent > 0 && model.CorrectAnswerPercent < 55)
            {
                result.Add(new StudentDiagnosisItemVm
                {
                    CssClass = "risk",
                    Icon = "❌",
                    Title = "خطر — دقة الإجابات منخفضة",
                    Body = "نسبة الإجابات الصحيحة " + model.CorrectAnswerPercent.ToString("0.##") + "%، وهذا مؤشر مباشر لاحتياج الطالب لخطة علاجية."
                });
            }

            if (model.RemedialPlansCount > 0 && model.CompletedRemedialPlansCount < model.RemedialPlansCount)
            {
                result.Add(new StudentDiagnosisItemVm
                {
                    CssClass = "opp",
                    Icon = "🎯",
                    Title = "فرصة تدخل — خطة علاجية مفتوحة",
                    Body = "لدى الطالب خطط علاجية غير مكتملة. الأفضل متابعة إتمامها وربطها بنتائج الاختبارات القادمة."
                });
            }

            if (result.Count == 0)
            {
                result.Add(new StudentDiagnosisItemVm
                {
                    CssClass = "str",
                    Icon = "✅",
                    Title = "حالة مستقرة",
                    Body = "لا توجد مؤشرات حرجة حالياً في ملف هذا الطالب."
                });
            }

            return result;
        }

        private static List<StudentHeatmapWeekVm> BuildStudentHeatmap(
            DateTime today,
            List<StudentAttendanceProjection> attendanceRows,
            List<StudentHomeworkProjection> homeworkRows,
            List<StudentExamProjection> examRows,
            List<StudentQuestionAttemptProjection> questionAttempts)
        {
            List<StudentHeatmapWeekVm> result = new List<StudentHeatmapWeekVm>();

            DateTime start = today.Date.AddDays(-20);

            for (int week = 0; week < 3; week++)
            {
                StudentHeatmapWeekVm weekVm = new StudentHeatmapWeekVm();

                for (int day = 0; day < 7; day++)
                {
                    DateTime current = start.AddDays((week * 7) + day);

                    int attendanceCount = attendanceRows.Count(x => x.RecordedAt.Date == current.Date || x.LectureDate.Date == current.Date);
                    int homeworkCount = homeworkRows.Count(x => x.AssignedAt.Date == current.Date || (x.SubmittedAt.HasValue && x.SubmittedAt.Value.Date == current.Date));
                    int examCount = examRows.Count(x => x.AssignedAt.Date == current.Date || (x.SubmittedAt.HasValue && x.SubmittedAt.Value.Date == current.Date));
                    int attemptCount = questionAttempts.Count(x => x.AttemptedAt.Date == current.Date);

                    int total = attendanceCount + homeworkCount + examCount + attemptCount;

                    string style = "background:var(--bg-2);color:var(--mute)";

                    if (total >= 12)
                        style = "background:#1d4ed8;color:#fff";
                    else if (total >= 8)
                        style = "background:#3b82f6;color:#fff";
                    else if (total >= 4)
                        style = "background:#93c5fd;color:#1e3a8a";
                    else if (total > 0)
                        style = "background:#dbeafe;color:#1e3a8a";

                    weekVm.Days.Add(new StudentHeatmapDayVm
                    {
                        ActivityCount = total,
                        CssStyle = style
                    });
                }

                result.Add(weekVm);
            }

            return result;
        }





        public IActionResult Create()
        {
            var viewModel = new StudentViewModel
            {
                Branches = GetBranches(),
                Batches = GetBatches(),
                Parents = GetParents(),
                Users = GetUsers() // اختيار المستخدم يدوي (اختياري)
            };

            return View(viewModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StudentViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .Select(x => new {
                        Field = x.Key,
                        Errors = x.Value.Errors.Select(e => e.ErrorMessage).ToList()
                    });

                foreach (var item in errors)
                {
                    Console.WriteLine($"❌ الحقل: {item.Field}");
                    foreach (var err in item.Errors)
                    {
                        Console.WriteLine($"    ➤ الخطأ: {err}");
                    }
                }

                // رجّع القوائم
                viewModel.Branches = GetBranches();
                viewModel.Batches = GetBatches();
                viewModel.Parents = GetParents();
                viewModel.Users = GetUsers();
                return View(viewModel);
            }

            if (ModelState.IsValid)
            {
                // ربط تلقائي بحساب مستخدم إن لم يتم اختياره
                if (string.IsNullOrEmpty(viewModel.UserId))
                {
                    var availableUser = _context.Users
                        .FirstOrDefault(u => !_context.Students.Any(s => s.UserId == u.Id));

                    if (availableUser != null)
                    {
                        viewModel.UserId = availableUser.Id;
                    }
                }

                var student = new Student
                {
                    NationalID = viewModel.NationalID,
                    FullName = viewModel.FullName,
                    Email = viewModel.Email,
                    PhoneNumber = viewModel.PhoneNumber,
                    WhatsAppNumber = viewModel.WhatsAppNumber,
                    Gender = viewModel.Gender,
                    School = viewModel.School,
                    Level = viewModel.Level,
                    BranchId = viewModel.BranchId,
                    ParentId = viewModel.ParentId,
                    EnrollmentStatus = viewModel.EnrollmentStatus,
                    UserId = viewModel.UserId
                };

                _context.Students.Add(student);
                await _context.SaveChangesAsync();


                try
                {
                    _context.Students.Add(student);
                    await _context.SaveChangesAsync();

                    // ✅ استدعاء الخدمة لإرسال الواجبات للطالب الجديد
                    var hasBatches = _context.StudentBatchEnrollments
            .Any(e => e.StudentID == student.StudentID);

                    if (hasBatches)
                    {
                        await _homeworkAssignmentService.AssignMissingHomeworksToStudentAsync(student.StudentID);
                    }


                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }

                return RedirectToAction(nameof(Index));
            }

      
            return View(viewModel);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            // هات كل دفعات الطالب عبر جدول الربط
            var batchIds = await (
                from e in _context.StudentBatchEnrollments
                where e.StudentID == id
                select e.BatchId
            ).ToListAsync();

            var viewModel = new StudentViewModel
            {
                StudentID = student.StudentID,
                NationalID = student.NationalID,
                FullName = student.FullName,
                Email = student.Email,
                PhoneNumber = student.PhoneNumber,
                WhatsAppNumber = student.WhatsAppNumber,
                Gender = student.Gender,
                School = student.School,
                Level = student.Level,
                BranchId = student.BranchId,
                ParentId = student.ParentId,
                EnrollmentStatus = student.EnrollmentStatus,
                UserId = student.UserId,

                // الجديد (متعدد الدفعات)
                BatchIds = batchIds,

                // القوائم
                Branches = GetBranches(),
                Batches = GetBatches(),
                Parents = GetParents(),
                Users = GetUsers()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StudentViewModel viewModel)
        {
            if (id != viewModel.StudentID) return NotFound();

            if (!ModelState.IsValid)
            {
                viewModel.Branches = GetBranches();
                viewModel.Batches = GetBatches();
                viewModel.Parents = GetParents();
                viewModel.Users = GetUsers();
                return View(viewModel);
            }

            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            // ===============================
            // تحديث البيانات الأساسية
            // ===============================
            student.NationalID = viewModel.NationalID;
            student.FullName = viewModel.FullName;
            student.Email = viewModel.Email;
            student.PhoneNumber = viewModel.PhoneNumber;
            student.WhatsAppNumber = viewModel.WhatsAppNumber;
            student.Gender = viewModel.Gender;
            student.School = viewModel.School;
            student.Level = viewModel.Level;
            student.BranchId = viewModel.BranchId;
            student.ParentId = viewModel.ParentId;
            student.EnrollmentStatus = viewModel.EnrollmentStatus;
            student.UserId = viewModel.UserId;

            await _context.SaveChangesAsync();

            // ===============================
            // تجهيز البيانات
            // ===============================
            var wanted = viewModel.BatchIds ?? new List<int>();

            var current = await _context.StudentBatchEnrollments
                .Where(e => e.StudentID == student.StudentID)
                .ToListAsync();

            // ===============================
            // حذف القديم (بدون Contains)
            // ===============================
            var rowsToRemove = new List<StudentBatchEnrollment>();

            foreach (var row in current)
            {
                bool stillWanted = false;

                foreach (var w in wanted)
                {
                    if (row.BatchId == w)
                    {
                        stillWanted = true;
                        break;
                    }
                }

                if (!stillWanted)
                {
                    rowsToRemove.Add(row);
                }
            }

            if (rowsToRemove.Count > 0)
            {
                _context.StudentBatchEnrollments.RemoveRange(rowsToRemove);
            }

            // ===============================
            // إضافة الجديد (بدون Contains / Except)
            // ===============================
            var newRows = new List<StudentBatchEnrollment>();

            foreach (var w in wanted)
            {
                bool exists = false;

                foreach (var c in current)
                {
                    if (c.BatchId == w)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    newRows.Add(new StudentBatchEnrollment
                    {
                        StudentID = id,
                        BatchId = w
                    });
                }
            }

            // ===============================
            // Bulk Insert
            // ===============================
            if (newRows.Count > 0)
            {
                await _context.BulkInsertAsync(newRows);
            }

            await _context.SaveChangesAsync();

            // ===============================
            // Assign Homeworks
            // ===============================
            bool hasBatches = false;

            foreach (var w in wanted)
            {
                hasBatches = true;
                break;
            }

            if (!hasBatches)
            {
                var anyEnrollment = await _context.StudentBatchEnrollments
                    .AnyAsync(e => e.StudentID == student.StudentID);

                hasBatches = anyEnrollment;
            }

            if (hasBatches)
            {
                await _homeworkAssignmentService
                    .AssignMissingHomeworksToStudentAsync(student.StudentID);
            }

            return RedirectToAction("Index", "Students", new { area = "Admin" });
        }
        public async Task<IActionResult> Delete(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
                return NotFound();

            var viewModel = new StudentViewModel
            {
                StudentID = student.StudentID,
                FullName = student.FullName,
                Email = student.Email,
                PhoneNumber = student.PhoneNumber
            };

            return View(viewModel);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
                return NotFound();

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }


        private List<SelectListItem> GetBranches()
        {
            return _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToList();
        }

        private List<SelectListItem> GetBatches()
        {
            return _context.Batches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToList();
        }

        private List<SelectListItem> GetParents()
        {
            return _context.Parents
                .Select(p => new SelectListItem
                {
                    Value = p.ParentID.ToString(),
                    Text = p.FullName
                }).ToList();
        }

        private List<SelectListItem> GetUsers()
        {
            return _context.Users
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = u.UserName // أو الاسم الكامل لو موجود
                }).ToList();
        }

        private class StudentBatchProjection
        {
            public int BatchId { get; set; }
            public string BatchName { get; set; } = "";
            public string CourseName { get; set; } = "";
            public string BranchName { get; set; } = "";
            public DateTime EnrolledAt { get; set; }
            public string Status { get; set; } = "";
        }

        private class StudentAttendanceProjection
        {
            public int LectureId { get; set; }
            public int BatchId { get; set; }
            public string BatchName { get; set; } = "";
            public DateTime LectureDate { get; set; }
            public bool IsPresent { get; set; }
            public DateTime RecordedAt { get; set; }
        }

        private class StudentHomeworkProjection
        {
            public int HomeworkSetId { get; set; }
            public string Title { get; set; } = "";
            public int BatchId { get; set; }
            public string BatchName { get; set; } = "";
            public DateTime AssignedAt { get; set; }
            public DateTime? SubmittedAt { get; set; }
            public DateTime? EndAt { get; set; }
            public bool IsSubmitted { get; set; }
            public double? Score { get; set; }
        }

        private class StudentExamProjection
        {
            public int StatusId { get; set; }
            public int? ExamAssignmentId { get; set; }
            public string Title { get; set; } = "";
            public int BatchId { get; set; }
            public string BatchName { get; set; } = "";
            public DateTime AssignedAt { get; set; }
            public DateTime? StartedAt { get; set; }
            public DateTime? SubmittedAt { get; set; }
            public bool IsSubmitted { get; set; }
            public int? Score { get; set; }
        }

        private class StudentPerformanceProjection
        {
            public int Id { get; set; }
            public DateTime ExamDate { get; set; }
            public double Score { get; set; }
            public string? WeakTopics { get; set; }
            public double EngagementScore { get; set; }
        }

        private class StudentQuestionAttemptProjection
        {
            public int Id { get; set; }

            public DateTime AttemptedAt { get; set; }

            public bool IsCorrect { get; set; }

            public int? SectionId { get; set; }

            public int? LessonId { get; set; }

            public double TimeTakenSeconds { get; set; }

            public bool IsMarkedForReview { get; set; }
        }

        private class StudentRemedialPlanProjection
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string? PerformanceLevel { get; set; }
            public DateTime CreatedAt { get; set; }
            public int TotalLessons { get; set; }
            public int CompletedLessons { get; set; }
            public bool IsCompleted { get; set; }
        }

        private class StudentProgressProjection
        {
            public int Id { get; set; }

            public DateTime Date { get; set; }

            public string Topic { get; set; } = "";

            public double ProgressPercentage { get; set; }

            public double Score { get; set; }
        }

        // ============================================================
        // دفعات الطالب
        // ============================================================

        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee")]
        public async Task<IActionResult> StudentBatches(int id)
        {
            var student = await _context.Students.AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentID == id);
            if (student == null) return NotFound();

            var enrollments = await (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join b in _context.Batches.AsNoTracking() on e.BatchId equals b.Id
                join c in _context.Courses.AsNoTracking() on b.CourseId equals c.Id into cj
                from c in cj.DefaultIfEmpty()
                where e.StudentID == id
                orderby e.EnrolledAt descending
                select new StudentBatchRowVm
                {
                    BatchId    = b.Id,
                    BatchName  = b.Name,
                    CourseName = c != null ? c.Name : "—",
                    StartDate  = b.StartDate,
                    EndDate    = b.EndDate,
                    Status     = e.Status ?? "—",
                    EnrolledAt = e.EnrolledAt
                }).ToListAsync();

            ViewBag.StudentName = student.FullName;
            ViewBag.StudentId   = id;
            return View(enrollments);
        }

        // ============================================================
        // سجل نشاط الطالب
        // ============================================================

        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee")]
        public async Task<IActionResult> ActivityLog(int id)
        {
            var student = await _context.Students.AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentID == id);
            if (student == null) return NotFound();

            var today = DateTime.Today;

            var hwActivity = await _context.HomeworkSetStudents.AsNoTracking()
                .Where(h => h.StudentId == id)
                .OrderByDescending(h => h.LastUpdated)
                .Take(30)
                .Select(h => new StudentActivityItemVm
                {
                    Icon    = h.IsSubmitted ? "✅" : "📝",
                    Type    = "واجب",
                    Title   = _context.HomeworkSets
                                  .Where(hs => hs.Id == h.HomeworkSetId)
                                  .Select(hs => hs.Title).FirstOrDefault() ?? "واجب",
                    Detail  = h.IsSubmitted
                                  ? $"تم التسليم — الدرجة: {(h.Score.HasValue ? h.Score.Value.ToString("0.#") : "غير محددة")}"
                                  : "لم يُسلَّم بعد",
                    Date    = h.LastUpdated,
                    CssTag  = h.IsSubmitted ? "tag-green" : "tag-amber"
                }).ToListAsync();

            var attendActivity = await _context.AttendanceRecords.AsNoTracking()
                .Where(a => a.StudentId == id)
                .OrderByDescending(a => a.RecordedAt)
                .Take(30)
                .Select(a => new StudentActivityItemVm
                {
                    Icon   = a.IsPresent ? "🗓" : "❌",
                    Type   = "حضور",
                    Title  = _context.Lecture
                                 .Where(l => l.Id == a.LectureId)
                                 .Select(l => l.Title).FirstOrDefault() ?? "محاضرة",
                    Detail = a.IsPresent ? "حاضر" : "غائب",
                    Date   = a.RecordedAt,
                    CssTag = a.IsPresent ? "tag-green" : "tag-red"
                }).ToListAsync();

            var examActivity = await (
                from s in _context.ExamStudentStatuses.AsNoTracking()
                join a in _context.ExamAssignmentsToBatches.AsNoTracking()
                    on s.ExamAssignmentId equals (int?)a.Id
                where s.StudentId == id
                orderby s.SubmittedAt descending
                select new StudentActivityItemVm
                {
                    Icon   = s.IsSubmitted ? "🏆" : "📋",
                    Type   = "اختبار",
                    Title  = a.Title ?? "اختبار",
                    Detail = s.IsSubmitted
                                 ? $"مُقدَّم — الدرجة: {(s.Score.HasValue ? s.Score.Value.ToString("0") : "غير محددة")}"
                                 : "لم يُقدَّم",
                    Date   = s.SubmittedAt ?? today,
                    CssTag = s.IsSubmitted ? "tag-blue" : "tag-amber"
                }).Take(30).ToListAsync();

            var allActivity = hwActivity
                .Concat(attendActivity)
                .Concat(examActivity)
                .OrderByDescending(a => a.Date)
                .ToList();

            ViewBag.StudentName     = student.FullName;
            ViewBag.StudentId       = id;
            ViewBag.LastLoginAt     = student.LastLoginAt;
            return View(allActivity);
        }

        // ============================================================
        // التقرير العام للطالب
        // ============================================================

        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee")]
        public async Task<IActionResult> GeneralReport(int id)
        {
            var student = await _context.Students.AsNoTracking()
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(s => s.StudentID == id);
            if (student == null) return NotFound();

            // ── دفعات الطالب مع الدورات ──
            var studentBatches = await (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join b in _context.Batches.AsNoTracking() on e.BatchId equals b.Id
                join c in _context.Courses.AsNoTracking() on b.CourseId equals c.Id into cj
                from c in cj.DefaultIfEmpty()
                where e.StudentID == id
                select new { b.Id, BatchName = b.Name, CourseName = c != null ? c.Name : "—", b.StartDate, b.EndDate, b.IsActive }
            ).ToListAsync();

            // ── الحضور مع تفصيل الدفعة ──
            var attendDetail = await (
                from a in _context.AttendanceRecords.AsNoTracking()
                join l in _context.Lecture.AsNoTracking() on a.LectureId equals l.Id
                where a.StudentId == id
                select new { l.BatchId, a.IsPresent, l.Date }
            ).ToListAsync();

            int totalLectures = attendDetail.Count;
            int presentCount  = attendDetail.Count(a => a.IsPresent);
            double attendPct  = totalLectures > 0 ? Math.Round((double)presentCount / totalLectures * 100, 1) : 0;

            // ── الواجبات مع تفصيل الدفعة ──
            var hwDetail = await (
                from hss in _context.HomeworkSetStudents.AsNoTracking()
                join hs in _context.HomeworkSets.AsNoTracking() on hss.HomeworkSetId equals hs.Id
                where hss.StudentId == id
                select new { hs.BatchId, hss.IsSubmitted, hss.Score }
            ).ToListAsync();

            int totalHw       = hwDetail.Count;
            int submittedHw   = hwDetail.Count(h => h.IsSubmitted);
            var scoredHw      = hwDetail.Where(h => h.Score.HasValue).ToList();
            double avgHwScore = scoredHw.Count > 0 ? Math.Round(scoredHw.Average(h => h.Score!.Value), 1) : 0;

            // ── الاختبارات مع تفصيل الدفعة ──
            var examDetail = await (
                from s2 in _context.ExamStudentStatuses.AsNoTracking()
                join a2 in _context.ExamAssignmentsToBatches.AsNoTracking()
                    on s2.ExamAssignmentId equals (int?)a2.Id
                where s2.StudentId == id
                select new { a2.BatchId, s2.IsSubmitted, s2.Score, s2.SubmittedAt }
            ).ToListAsync();

            int totalExams      = examDetail.Count;
            int submittedExams  = examDetail.Count(e => e.IsSubmitted);
            var scoredExams     = examDetail.Where(e => e.Score.HasValue).ToList();
            double avgExamScore = scoredExams.Count > 0 ? Math.Round(scoredExams.Average(e => e.Score!.Value), 1) : 0;

            int batchCount  = studentBatches.Count;
            int courseCount = studentBatches.Select(b => b.CourseName).Distinct().Count();

            // ── درجة الصحة العامة ──
            double hwPctGlobal = totalHw > 0 ? Math.Round((double)submittedHw / totalHw * 100, 1) : 0;
            double exPctGlobal = totalExams > 0 ? Math.Round((double)submittedExams / totalExams * 100, 1) : 0;
            double healthScore = Math.Min(100, Math.Round(
                (attendPct * 0.35) + (hwPctGlobal * 0.35) + (exPctGlobal * 0.30), 1));

            // ── إحصاءات الشهور الستة الأخيرة (للرسم البياني) ──
            var sixMonths = Enumerable.Range(0, 6)
                .Select(i => DateTime.Today.AddMonths(-5 + i))
                .ToList();

            var monthlyAttend = sixMonths.Select(m => attendDetail.Count(
                a => a.Date.Year == m.Year && a.Date.Month == m.Month && a.IsPresent)).ToList();

            var monthlyExam = sixMonths.Select(m => examDetail.Count(
                e => e.SubmittedAt.HasValue && e.SubmittedAt.Value.Year == m.Year && e.SubmittedAt.Value.Month == m.Month)).ToList();

            // ── تفصيل الدفعات (لكل دفعة: حضور، واجب، اختبار) ──
            var batchBreakdown = studentBatches.Select(b =>
            {
                var bAtt  = attendDetail.Where(a => a.BatchId == b.Id).ToList();
                var bHw   = hwDetail.Where(h => h.BatchId == b.Id).ToList();
                var bExam = examDetail.Where(e => e.BatchId == b.Id).ToList();
                int bTotal = bAtt.Count;
                int bPres  = bAtt.Count(a => a.IsPresent);
                double bAttPct = bTotal > 0 ? Math.Round((double)bPres / bTotal * 100, 0) : 0;
                double bHwPct  = bHw.Count > 0 ? Math.Round((double)bHw.Count(h => h.IsSubmitted) / bHw.Count * 100, 0) : 0;
                double bExPct  = bExam.Count > 0 ? Math.Round((double)bExam.Count(e => e.IsSubmitted) / bExam.Count * 100, 0) : 0;
                return new BatchBreakdownRowVm
                {
                    BatchName   = b.BatchName,
                    CourseName  = b.CourseName,
                    TotalLec    = bTotal,
                    Present     = bPres,
                    Absent      = bTotal - bPres,
                    AttPct      = bAttPct,
                    HwPct       = bHwPct,
                    ExPct       = bExPct,
                    TotalHw     = bHw.Count,
                    SubmittedHw = bHw.Count(h => h.IsSubmitted),
                    TotalEx     = bExam.Count,
                    SubmittedEx = bExam.Count(e => e.IsSubmitted),
                    Health      = Math.Round((bAttPct * 0.4) + (bHwPct * 0.3) + (bExPct * 0.3), 0)
                };
            }).ToList();

            // ── AI توصية بناءً على البيانات ──
            string aiRec = BuildAIRecommendation(attendPct, hwPctGlobal, exPctGlobal, avgExamScore, avgHwScore, healthScore);

            ViewBag.StudentName      = student.FullName;
            ViewBag.StudentId        = id;
            ViewBag.BranchName       = student.Branch?.Name ?? "—";
            ViewBag.EnrollmentStatus = student.EnrollmentStatus;
            ViewBag.RegistrationDate = student.RegistrationDate;
            ViewBag.LastLoginAt      = student.LastLoginAt;

            ViewBag.TotalLectures    = totalLectures;
            ViewBag.PresentCount     = presentCount;
            ViewBag.AttendPct        = attendPct;

            ViewBag.TotalHw          = totalHw;
            ViewBag.SubmittedHw      = submittedHw;
            ViewBag.AvgHwScore       = avgHwScore;

            ViewBag.TotalExams       = totalExams;
            ViewBag.SubmittedExams   = submittedExams;
            ViewBag.AvgExamScore     = avgExamScore;

            ViewBag.BatchCount       = batchCount;
            ViewBag.CourseCount      = courseCount;
            ViewBag.HealthScore      = healthScore;
            ViewBag.HwPct            = hwPctGlobal;
            ViewBag.ExPct            = exPctGlobal;

            ViewBag.MonthLabels      = sixMonths.Select(m => m.ToString("MMM yyyy")).ToList();
            ViewBag.MonthlyAttend    = monthlyAttend;
            ViewBag.MonthlyExam      = monthlyExam;

            ViewBag.BatchBreakdown   = (List<BatchBreakdownRowVm>)batchBreakdown;
            ViewBag.AIRecommendation = aiRec;

            return View();
        }

        private static string BuildAIRecommendation(double attendPct, double hwPct, double exPct, double avgExam, double avgHw, double health)
        {
            var parts = new System.Text.StringBuilder();
            parts.Append("بناءً على تحليل بيانات الطالب، ");

            if (health >= 80)
                parts.Append("يُظهر الطالب مستوى تعليمياً ممتازاً وأداءً مستقراً عبر جميع محاور التقييم. ");
            else if (health >= 65)
                parts.Append("يسير الطالب في مسار جيد مع وجود نقاط يمكن تعزيزها. ");
            else if (health >= 50)
                parts.Append("يحتاج الطالب إلى متابعة مباشرة لتحسين مستواه العام. ");
            else
                parts.Append("يعاني الطالب من ضعف واضح في مؤشرات الأداء ويستلزم تدخلاً فورياً. ");

            if (attendPct < 70)
                parts.Append($"نسبة الحضور منخفضة ({attendPct:0.#}%) وتؤثر سلباً على استيعابه — يُنصح بمتابعة أسباب الغياب والتواصل مع ولي الأمر. ");
            else if (attendPct >= 90)
                parts.Append($"نسبة الحضور ممتازة ({attendPct:0.#}%) تعكس التزاماً عالياً بالمنهج. ");

            if (hwPct < 60)
                parts.Append($"تسليم الواجبات منخفض ({hwPct:0.#}%) مما يُشير إلى عدم الالتزام بالمتابعة المنزلية — يُوصى بإرسال تذكيرات وتحديد موعد للمراجعة. ");
            else if (hwPct >= 85)
                parts.Append($"الطالب ملتزم بتسليم الواجبات ({hwPct:0.#}%) ويدل على جدية في الدراسة المستقلة. ");

            if (avgExam > 0 && avgExam < 60)
                parts.Append($"متوسط درجات الاختبارات ({avgExam:0.#}) يقل عن المقبول — يُنصح بمراجعة المحاور الضعيفة ووضع خطة علاجية مخصصة. ");
            else if (avgExam >= 80)
                parts.Append($"متوسط درجات الاختبارات مرتفع ({avgExam:0.#}) يؤكد إتقانه للمادة العلمية. ");

            if (health < 50)
                parts.Append("التوصية الإجمالية: يجب اتخاذ إجراء عاجل يشمل اجتماعاً مع الطالب وولي أمره، ووضع خطة تصحيحية واضحة وقابلة للقياس.");
            else if (health < 70)
                parts.Append("التوصية الإجمالية: يحتاج الطالب دعماً إضافياً في النقاط المذكورة أعلاه مع متابعة دورية كل أسبوعين.");
            else
                parts.Append("التوصية الإجمالية: استمر في دعم الطالب وتشجيعه للحفاظ على هذا المستوى أو تطويره.");

            return parts.ToString();
        }

        // StudentBatchRowVm and StudentActivityItemVm moved to ViewModels/Students/StudentQuickViewModels.cs

        // ============================================================
        // تفاصيل الحضور والغياب للطالب
        // ============================================================

        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee")]
        public async Task<IActionResult> StudentAttendanceReport(int id)
        {
            var student = await _context.Students.AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentID == id);
            if (student == null) return NotFound();

            var rows = await (
                from a in _context.AttendanceRecords.AsNoTracking()
                join l in _context.Lecture.AsNoTracking() on a.LectureId equals l.Id
                join b in _context.Batches.AsNoTracking() on l.BatchId equals b.Id
                join c in _context.Courses.AsNoTracking() on b.CourseId equals c.Id into cj
                from c in cj.DefaultIfEmpty()
                where a.StudentId == id
                orderby l.Date descending
                select new StudentAttendanceRowVm
                {
                    LectureId     = l.Id,
                    LectureTitle  = l.Title,
                    LectureDate   = l.Date,
                    BatchId       = b.Id,
                    BatchName     = b.Name,
                    CourseName    = c != null ? c.Name : "—",
                    IsPresent     = a.IsPresent,
                    RecordedAt    = a.RecordedAt
                }).ToListAsync();

            ViewBag.StudentName   = student.FullName;
            ViewBag.StudentId     = id;
            ViewBag.TotalLec      = rows.Count;
            ViewBag.PresentCount  = rows.Count(r => r.IsPresent);
            return View(rows);
        }

        // ============================================================
        // تفاصيل الواجبات للطالب
        // ============================================================

        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee")]
        public async Task<IActionResult> StudentHomeworksReport(int id)
        {
            var student = await _context.Students.AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentID == id);
            if (student == null) return NotFound();

            var rows = await (
                from hss in _context.HomeworkSetStudents.AsNoTracking()
                join hs in _context.HomeworkSets.AsNoTracking() on hss.HomeworkSetId equals hs.Id
                join b in _context.Batches.AsNoTracking() on hs.BatchId equals b.Id
                join c in _context.Courses.AsNoTracking() on b.CourseId equals c.Id into cj
                from c in cj.DefaultIfEmpty()
                where hss.StudentId == id
                orderby hss.AssignedAt descending
                select new StudentHomeworkRowVm
                {
                    HomeworkSetId   = hs.Id,
                    Title           = hs.Title,
                    BatchId         = b.Id,
                    BatchName       = b.Name,
                    CourseName      = c != null ? c.Name : "—",
                    AssignedAt      = hss.AssignedAt,
                    EndAt           = hs.EndAt,
                    IsSubmitted     = hss.IsSubmitted,
                    SubmittedAt     = hss.SubmittedAt,
                    Score           = hss.Score,
                    IsClosed        = hs.IsClosed
                }).ToListAsync();

            ViewBag.StudentName   = student.FullName;
            ViewBag.StudentId     = id;
            ViewBag.Total         = rows.Count;
            ViewBag.Submitted     = rows.Count(r => r.IsSubmitted);
            ViewBag.AvgScore      = rows.Where(r => r.Score.HasValue).Any()
                ? Math.Round(rows.Where(r => r.Score.HasValue).Average(r => r.Score!.Value), 1) : 0.0;
            return View(rows);
        }

        // ============================================================
        // تفاصيل الاختبارات للطالب
        // ============================================================

        [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee")]
        public async Task<IActionResult> StudentExamsReport(int id)
        {
            var student = await _context.Students.AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentID == id);
            if (student == null) return NotFound();

            var rows = await (
                from s in _context.ExamStudentStatuses.AsNoTracking()
                join a in _context.ExamAssignmentsToBatches.AsNoTracking()
                    on s.ExamAssignmentId equals (int?)a.Id
                join b in _context.Batches.AsNoTracking() on a.BatchId equals b.Id
                join c in _context.Courses.AsNoTracking() on b.CourseId equals c.Id into cj
                from c in cj.DefaultIfEmpty()
                where s.StudentId == id
                orderby s.SubmittedAt descending
                select new StudentExamRowVm
                {
                    ExamAssignmentId = a.Id,
                    Title            = a.Title ?? "اختبار",
                    BatchId          = b.Id,
                    BatchName        = b.Name,
                    CourseName       = c != null ? c.Name : "—",
                    CreatedAt        = a.CreatedAt,
                    IsSubmitted      = s.IsSubmitted,
                    SubmittedAt      = s.SubmittedAt,
                    Score            = s.Score
                }).ToListAsync();

            ViewBag.StudentName   = student.FullName;
            ViewBag.StudentId     = id;
            ViewBag.Total         = rows.Count;
            ViewBag.Submitted     = rows.Count(r => r.IsSubmitted);
            ViewBag.AvgScore      = rows.Where(r => r.Score.HasValue).Any()
                ? Math.Round(rows.Where(r => r.Score.HasValue).Average(r => r.Score!.Value), 1) : 0.0;
            return View(rows);
        }

    }
}
