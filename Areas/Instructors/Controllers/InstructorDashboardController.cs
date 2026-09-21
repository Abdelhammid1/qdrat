// ─── مسار: Areas/Instructors/Controllers/InstructorDashboardController.cs ───

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Instructor;
using System.Globalization;
using System.Text.Json;

namespace QdratNew.Areas.Instructors.Controllers
{
    [Area("Instructors")]
    public class InstructorDashboardController : BaseInstructorController
    {
        private readonly ApplicationDbContext _context;

        public InstructorDashboardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
            : base(userManager, scopeService)
        {
            _context = context;
        }

        // ══════════════════════════════════════════════════
        // 🏠 الداشبورد الرئيسي
        // ══════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            var vm = await BuildDashboardViewModelAsync(instructorId);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> StatDetails(string metric = "students")
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            var dashboard = await BuildDashboardViewModelAsync(instructorId);
            var normalizedMetric = string.IsNullOrWhiteSpace(metric)
                ? "students"
                : metric.Trim().ToLowerInvariant();

            var vm = BuildStatDetailsViewModel(normalizedMetric, dashboard);
            return View("~/Areas/Instructors/Views/InstructorDashboard/StatDetails.cshtml", vm);
        }

        // ══════════════════════════════════════════════════
        // 📡 API: إحصائيات سريعة (AJAX)
        // ══════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> GetQuickStats()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            var now = DateTime.Now;
            var allowedBatchIds = await GetAllowedBatchIdsAsync(instructorId);
            var allowedStudentIds = await GetStudentIdsForBatchesAsync(allowedBatchIds);

            var totalStudents = allowedStudentIds.Count;

            var examAssignmentsRaw = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Select(x => new { x.Id, x.BatchId })
                .ToListAsync();

            var instructorExamAssignments = examAssignmentsRaw
                .Where(x => IsAllowedId(allowedBatchIds, x.BatchId))
                .ToList();

            var totalExams = instructorExamAssignments.Count;

            var homeworkSetsRaw = await _context.HomeworkSets
                .AsNoTracking()
                .Select(x => new { x.Id, x.BatchId, x.EndAt })
                .ToListAsync();

            var instructorHomeworkSets = homeworkSetsRaw
                .Where(x => IsAllowedId(allowedBatchIds, x.BatchId))
                .ToList();

            var totalHomeworks = instructorHomeworkSets.Count;
            var activeHomeworks = instructorHomeworkSets.Count(x => IsHomeworkActive(x.EndAt, now));

            var homeworkSetStudentsRaw = await _context.HomeworkSetStudents
                .AsNoTracking()
                .Select(x => new { x.HomeworkSetId, x.StudentId, x.IsSubmitted })
                .ToListAsync();

            var scopedHomeworkStudents = homeworkSetStudentsRaw
                .Where(x =>
                    instructorHomeworkSets.Any(h => h.Id == x.HomeworkSetId) &&
                    IsAllowedId(allowedStudentIds, x.StudentId))
                .ToList();

            var completionRate = scopedHomeworkStudents.Count > 0
                ? (int)Math.Round(scopedHomeworkStudents.Count(x => x.IsSubmitted) * 100.0 / scopedHomeworkStudents.Count)
                : 0;

            return Json(new
            {
                totalStudents,
                totalExams,
                totalHomeworks,
                activeHomeworks,
                completionRate
            });
        }

        // ══════════════════════════════════════════════════
        // 📡 API: الاختبارات الأخيرة (AJAX)
        // ══════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> GetRecentExams(int count = 5)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            count = NormalizeTakeCount(count);
            var allowedBatchIds = await GetAllowedBatchIdsAsync(instructorId);

            // 1) اختبارات الدفعات العادية
            var batchExamsRaw = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.BatchId,
                    BatchName = x.Batch != null ? x.Batch.Name : "-",
                    x.ScheduledDate,
                    x.DurationMinutes,
                    x.CreatedAt
                })
                .ToListAsync();

            var scopedBatchExams = batchExamsRaw
                .Where(x => IsAllowedId(allowedBatchIds, x.BatchId))
                .OrderByDescending(x => x.CreatedAt)
                .Take(count)
                .ToList();

            var examStatusesRaw = await _context.ExamStudentStatuses
                .AsNoTracking()
                .Select(x => new { x.ExamAssignmentId, x.IsSubmitted })
                .ToListAsync();

            var batchExamItems = scopedBatchExams.Select(x =>
            {
                var relatedStatuses = examStatusesRaw
                    .Where(s => s.ExamAssignmentId.HasValue && s.ExamAssignmentId.Value == x.Id)
                    .ToList();

                return new DashboardExamApiItem
                {
                    examAssignmentId = x.Id,
                    title = x.Title,
                    examType = "Batch",
                    targetName = x.BatchName,
                    startAt = x.ScheduledDate,
                    endAt = CalculateExamEndAt(x.ScheduledDate, x.DurationMinutes),
                    durationMinutes = x.DurationMinutes,
                    createdAt = x.CreatedAt,
                    submittedCount = relatedStatuses.Count(s => s.IsSubmitted),
                    totalCount = relatedStatuses.Count
                };
            }).ToList();

            // 2) اختبارات مؤشرات الأداء
            var performanceLinksRaw = await _context.Set<PerformanceIndicatorExamToBatch>()
                .AsNoTracking()
                .Select(x => new { x.PerformanceIndicatorExamId, x.BatchId })
                .ToListAsync();

            var allowedPerformanceExamIds = performanceLinksRaw
                .Where(x => IsAllowedId(allowedBatchIds, x.BatchId))
                .Select(x => x.PerformanceIndicatorExamId)
                .Distinct()
                .ToList();

            var performanceExamsRaw = await _context.PerformanceIndicatorExams
                .AsNoTracking()
                .Select(x => new { x.Id, x.Title, x.StartAt, x.EndAt, x.DurationMinutes, x.CreatedAt })
                .ToListAsync();

            var performanceStudentsRaw = await _context.Set<PerformanceIndicatorExamStudent>()
                .AsNoTracking()
                .Select(x => new { x.PerformanceIndicatorExamId, x.IsCompleted })
                .ToListAsync();

            var performanceExamItems = performanceExamsRaw
                .Where(x => IsAllowedId(allowedPerformanceExamIds, x.Id))
                .OrderByDescending(x => x.CreatedAt)
                .Take(count)
                .Select(x =>
                {
                    var relatedStudents = performanceStudentsRaw
                        .Where(s => s.PerformanceIndicatorExamId == x.Id)
                        .ToList();

                    return new DashboardExamApiItem
                    {
                        examAssignmentId = x.Id,
                        title = x.Title,
                        examType = "Performance",
                        targetName = "دفعات متعددة",
                        startAt = x.StartAt,
                        endAt = x.EndAt,
                        durationMinutes = x.DurationMinutes,
                        createdAt = x.CreatedAt,
                        submittedCount = relatedStudents.Count(s => s.IsCompleted),
                        totalCount = relatedStudents.Count
                    };
                })
                .ToList();

            // 3) دمج النتائج
            var combined = batchExamItems
                .Concat(performanceExamItems)
                .OrderByDescending(x => x.createdAt)
                .Take(count)
                .ToList();

            return Json(combined);
        }

        // ══════════════════════════════════════════════════
        // 📡 API: الواجبات النشطة (AJAX)
        // ══════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> GetActiveHomeworks(int count = 5)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            count = NormalizeTakeCount(count);
            var now = DateTime.Now;
            var allowedBatchIds = await GetAllowedBatchIdsAsync(instructorId);
            var allowedStudentIds = await GetStudentIdsForBatchesAsync(allowedBatchIds);

            var homeworkSetsRaw = await _context.HomeworkSets
                .AsNoTracking()
                .Select(x => new { x.Id, x.BatchId, x.Title, x.StartAt, x.EndAt, x.CreatedAt })
                .ToListAsync();

            var scopedHomeworkSets = homeworkSetsRaw
                .Where(x => IsAllowedId(allowedBatchIds, x.BatchId))
                .OrderByDescending(x => x.CreatedAt)
                .Take(count)
                .ToList();

            var homeworkSetStudentsRaw = await _context.HomeworkSetStudents
                .AsNoTracking()
                .Select(x => new { x.HomeworkSetId, x.StudentId, x.IsSubmitted })
                .ToListAsync();

            var result = scopedHomeworkSets.Select(x =>
            {
                var relatedStudents = homeworkSetStudentsRaw
                    .Where(s => s.HomeworkSetId == x.Id && IsAllowedId(allowedStudentIds, s.StudentId))
                    .ToList();

                return new
                {
                    homeworkSetId = x.Id,
                    title = x.Title,
                    isClosed = IsHomeworkClosed(x.EndAt, now),
                    endAt = x.EndAt,
                    startAt = x.StartAt,
                    createdAt = x.CreatedAt,
                    totalStudents = relatedStudents.Count,
                    submittedCount = relatedStudents.Count(s => s.IsSubmitted)
                };
            }).ToList();

            return Json(result);
        }

        // ══════════════════════════════════════════════════
        // 📡 API: أداء المحاور (AJAX - Chart Data)
        // ══════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> GetSectionPerformance()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            var allowedBatchIds = await GetAllowedBatchIdsAsync(instructorId);

            var examAssignmentsRaw = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Select(x => new { x.Id, x.BatchId, x.CreatedAt })
                .ToListAsync();

            var lastExam = examAssignmentsRaw
                .Where(x => IsAllowedId(allowedBatchIds, x.BatchId))
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (lastExam == null)
                return Json(new { labels = Array.Empty<string>(), correct = Array.Empty<int>(), wrong = Array.Empty<int>() });

            var attemptsRaw = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(x => x.ExamAssignmentId == lastExam.Id)
                .Select(x => new { x.QuestionId, x.IsCorrect })
                .ToListAsync();

            if (!attemptsRaw.Any())
                return Json(new { labels = Array.Empty<string>(), correct = Array.Empty<int>(), wrong = Array.Empty<int>() });

            var questionIds = attemptsRaw.Select(x => x.QuestionId).Distinct().ToList();

            var questionsRaw = await _context.Questions
                .AsNoTracking()
                .Select(x => new { x.Id, x.LessonId })
                .ToListAsync();

            var lessonsRaw = await _context.Lessons
                .AsNoTracking()
                .Select(x => new { x.Id, x.SectionId })
                .ToListAsync();

            var directCurriculumIds = await GetInstructorDirectCurriculumIdsAsync(instructorId);

            var sectionsRaw = await _context.Sections
                .AsNoTracking()
                .Where(x => directCurriculumIds.Contains(x.CurriculumId))
                .Select(x => new { x.Id, x.Title })
                .ToListAsync();

            var accumulator = new Dictionary<string, SectionPerformanceAccumulator>();

            foreach (var attempt in attemptsRaw)
            {
                var question = questionsRaw.FirstOrDefault(q => q.Id == attempt.QuestionId);
                if (question == null) continue;

                var lesson = lessonsRaw.FirstOrDefault(l => l.Id == question.LessonId);
                if (lesson == null) continue;

                var section = sectionsRaw.FirstOrDefault(s => s.Id == lesson.SectionId);
                if (section == null) continue;

                var sectionName = section.Title ?? "غير محدد";
                if (!accumulator.TryGetValue(sectionName, out var acc))
                {
                    acc = new SectionPerformanceAccumulator();
                    accumulator[sectionName] = acc;
                }

                acc.Total++;
                if (attempt.IsCorrect) acc.Correct++;
                else acc.Wrong++;
            }

            var labels = accumulator.Keys.ToList();
            var correct = accumulator.Values
                .Select(a => a.Total > 0 ? (int)Math.Round(a.Correct * 100.0 / a.Total) : 0)
                .ToList();
            var wrong = accumulator.Values
                .Select(a => a.Total > 0 ? (int)Math.Round(a.Wrong * 100.0 / a.Total) : 0)
                .ToList();

            return Json(new { labels, correct, wrong });
        }

        // ══════════════════════════════════════════════════
        // 📡 API: أفضل الطلاب (AJAX)
        // ══════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> GetTopStudents(int count = 5)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            count = NormalizeTakeCount(count);
            var allowedBatchIds = await GetAllowedBatchIdsAsync(instructorId);
            var allowedStudentIds = await GetStudentIdsForBatchesAsync(allowedBatchIds);

            var examStatusesRaw = await _context.ExamStudentStatuses
                .AsNoTracking()
                .Select(x => new { x.StudentId, x.IsSubmitted, x.Note })
                .ToListAsync();

            var studentsRaw = await _context.Students
                .AsNoTracking()
                .Select(x => new { x.StudentID, x.FullName })
                .ToListAsync();

            var scopedStatuses = examStatusesRaw
                .Where(x => IsAllowedId(allowedStudentIds, x.StudentId) && x.IsSubmitted)
                .ToList();

            var result = scopedStatuses
                .GroupBy(x => x.StudentId)
                .Select(g =>
                {
                    var scores = g
                        .Select(s => ExtractScorePercent(s.Note))
                        .Where(s => s > 0)
                        .ToList();

                    var student = studentsRaw.FirstOrDefault(s => s.StudentID == g.Key);

                    return new
                    {
                        studentId = g.Key,
                        studentName = student?.FullName ?? $"طالب #{g.Key}",
                        examCount = g.Count(),
                        avgScore = scores.Any() ? Math.Round(scores.Average(), 1) : 0.0
                    };
                })
                .OrderByDescending(x => x.avgScore)
                .Take(count)
                .ToList();

            return Json(result);
        }

        // ══════════════════════════════════════════════════
        // 📡 API: نشاط الأسبوع (AJAX - Activity Chart)
        // ══════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> GetWeeklyActivity()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            var allowedBatchIds = await GetAllowedBatchIdsAsync(instructorId);
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7);

            var examsThisWeekRaw = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Where(x => x.CreatedAt >= startOfWeek && x.CreatedAt < endOfWeek)
                .Select(x => new { x.BatchId, x.CreatedAt })
                .ToListAsync();

            var scopedExamsThisWeek = examsThisWeekRaw
                .Where(x => IsAllowedId(allowedBatchIds, x.BatchId))
                .GroupBy(x => x.CreatedAt.Date)
                .Select(g => new { date = g.Key, count = g.Count() })
                .ToList();

            var homeworksThisWeekRaw = await _context.HomeworkSets
                .AsNoTracking()
                .Where(x => x.CreatedAt >= startOfWeek && x.CreatedAt < endOfWeek)
                .Select(x => new { x.BatchId, x.CreatedAt })
                .ToListAsync();

            var scopedHomeworksThisWeek = homeworksThisWeekRaw
                .Where(x => IsAllowedId(allowedBatchIds, x.BatchId))
                .GroupBy(x => x.CreatedAt.Date)
                .Select(g => new { date = g.Key, count = g.Count() })
                .ToList();

            var days = Enumerable.Range(0, 7)
                .Select(i => startOfWeek.AddDays(i))
                .Select(day => new
                {
                    dayName = day.ToString("dddd", new CultureInfo("ar-SA")),
                    exams = scopedExamsThisWeek.FirstOrDefault(x => x.date == day)?.count ?? 0,
                    homeworks = scopedHomeworksThisWeek.FirstOrDefault(x => x.date == day)?.count ?? 0
                })
                .ToList();

            return Json(days);
        }

        // ══════════════════════════════════════════════════
        // 📡 API: تنبيهات (AJAX)
        // ══════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> GetAlerts()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            var now = DateTime.Now;
            var next24Hours = now.AddHours(24);
            var allowedBatchIds = await GetAllowedBatchIdsAsync(instructorId);
            var alerts = new List<object>();

            var homeworkSetsRaw = await _context.HomeworkSets
                .AsNoTracking()
                .Select(x => new { x.BatchId, x.EndAt })
                .ToListAsync();

            var closingSoon = homeworkSetsRaw
                .Count(x => IsAllowedId(allowedBatchIds, x.BatchId)
                    && x.EndAt.HasValue
                    && x.EndAt.Value <= next24Hours
                    && x.EndAt.Value > now);

            if (closingSoon > 0)
                alerts.Add(new { type = "warning", message = $"يوجد {closingSoon} واجب ينتهي خلال 24 ساعة", action = "GetActiveHomeworks" });

            var examAssignmentsRaw = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Select(x => new { x.BatchId, x.ScheduledDate, x.DurationMinutes })
                .ToListAsync();

            var liveExams = examAssignmentsRaw.Count(x =>
            {
                if (!IsAllowedId(allowedBatchIds, x.BatchId)) return false;
                if (!x.ScheduledDate.HasValue) return false;
                var examEndAt = CalculateExamEndAt(x.ScheduledDate, x.DurationMinutes);
                return x.ScheduledDate.Value <= now && examEndAt.HasValue && examEndAt.Value >= now;
            });

            if (liveExams > 0)
                alerts.Add(new { type = "danger", message = $"{liveExams} اختبار جارٍ الآن", action = "LiveMonitor" });

            var pendingDrafts = await _context.ExamDrafts
                .AsNoTracking()
                .Where(x => x.CreatedByInstructorId == instructorId && !x.IsArchived)
                .CountAsync();

            if (pendingDrafts > 0)
                alerts.Add(new { type = "neutral", message = $"{pendingDrafts} مسودة اختبار لم تُرسَل بعد", action = "Drafts" });

            return Json(alerts);
        }

        // ══════════════════════════════════════════════════
        // 🔧 Helper: بناء الـ ViewModel الرئيسي
        // ══════════════════════════════════════════════════
        private async Task<InstructorDashboardViewModel> BuildDashboardViewModelAsync(int instructorId)
        {
            var now = DateTime.Now;
            var allowedBatchIds = await GetAllowedBatchIdsAsync(instructorId);
            var allowedStudentIds = await GetStudentIdsForBatchesAsync(allowedBatchIds);

            var totalStudents = allowedStudentIds.Count;

            // ── الاختبارات ──
            var examAssignmentsRaw = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.BatchId,
                    BatchName = x.Batch != null ? x.Batch.Name : "-",
                    x.ScheduledDate,
                    x.DurationMinutes,
                    x.CreatedAt
                })
                .ToListAsync();

            var instructorExamAssignments = examAssignmentsRaw
                .Where(x => IsAllowedId(allowedBatchIds, x.BatchId))
                .ToList();

            var totalExamsSent = instructorExamAssignments.Count;

            var examStatusesRaw = await _context.ExamStudentStatuses
                .AsNoTracking()
                .Select(x => new { x.ExamAssignmentId, x.IsSubmitted, x.Note })
                .ToListAsync();

            var recentExams = instructorExamAssignments
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(x =>
                {
                    var statuses = examStatusesRaw
                        .Where(s => s.ExamAssignmentId.HasValue && s.ExamAssignmentId.Value == x.Id)
                        .ToList();
                    return new DashboardExamItemVM
                    {
                        ExamAssignmentId = x.Id,
                        Title = x.Title,
                        ExamType = "Batch",
                        TargetName = x.BatchName,
                        StartAt = x.ScheduledDate,
                        EndAt = CalculateExamEndAt(x.ScheduledDate, x.DurationMinutes),
                        DurationMinutes = x.DurationMinutes,
                        CreatedAt = x.CreatedAt,
                        SubmittedCount = statuses.Count(s => s.IsSubmitted),
                        TotalCount = statuses.Count
                    };
                })
                .ToList();

            // ── الواجبات ──
            var homeworkSetsRaw = await _context.HomeworkSets
                .AsNoTracking()
                .Select(x => new { x.Id, x.BatchId, x.Title, x.StartAt, x.EndAt, x.CreatedAt })
                .ToListAsync();

            var instructorHomeworkSets = homeworkSetsRaw
                .Where(x => IsAllowedId(allowedBatchIds, x.BatchId))
                .ToList();

            var totalHomeworks = instructorHomeworkSets.Count;
            var activeHomeworks = instructorHomeworkSets.Count(x => IsHomeworkActive(x.EndAt, now));

            var homeworkSetStudentsRaw = await _context.HomeworkSetStudents
                .AsNoTracking()
                .Select(x => new { x.HomeworkSetId, x.StudentId, x.IsSubmitted })
                .ToListAsync();

            var scopedHomeworkStudents = homeworkSetStudentsRaw
                .Where(x =>
                    instructorHomeworkSets.Any(h => h.Id == x.HomeworkSetId) &&
                    IsAllowedId(allowedStudentIds, x.StudentId))
                .ToList();

            var completionRate = scopedHomeworkStudents.Count > 0
                ? (int)Math.Round(scopedHomeworkStudents.Count(x => x.IsSubmitted) * 100.0 / scopedHomeworkStudents.Count)
                : 0;

            var recentHomeworks = instructorHomeworkSets
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(x =>
                {
                    var students = homeworkSetStudentsRaw
                        .Where(s => s.HomeworkSetId == x.Id && IsAllowedId(allowedStudentIds, s.StudentId))
                        .ToList();
                    return new DashboardHomeworkItemVM
                    {
                        HomeworkSetId = x.Id,
                        Title = x.Title,
                        StartAt = x.StartAt,
                        EndAt = x.EndAt,
                        CreatedAt = x.CreatedAt,
                        TotalStudents = students.Count,
                        SubmittedCount = students.Count(s => s.IsSubmitted)
                    };
                })
                .ToList();

            // ── الدفعات ──
            var batchStudentsRaw = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Select(x => new { x.BatchId, x.StudentID })
                .ToListAsync();

            var batchesRaw = await _context.Batches
                .AsNoTracking()
                .Select(x => new { x.Id, x.Name })
                .ToListAsync();

            var batches = batchesRaw
                .Where(x => IsAllowedId(allowedBatchIds, x.Id))
                .Select(x => new DashboardBatchVM
                {
                    BatchId = x.Id,
                    BatchName = x.Name,
                    StudentCount = batchStudentsRaw.Count(s => s.BatchId == x.Id && IsAllowedId(allowedStudentIds, s.StudentID))
                })
                .ToList();

            // ── متوسط درجة الاختبارات ──
            var allScores = examStatusesRaw
                .Where(x => x.ExamAssignmentId.HasValue &&
                    instructorExamAssignments.Any(e => e.Id == x.ExamAssignmentId.Value))
                .Select(x => ExtractScorePercent(x.Note))
                .Where(s => s > 0)
                .ToList();

            var avgExamScore = allScores.Any() ? Math.Round(allScores.Average(), 1) : 0.0;

            // ── التنبيهات ──
            var alerts = new List<DashboardAlertVM>();
            var next24 = now.AddHours(24);

            var closingSoon = instructorHomeworkSets.Count(x =>
                x.EndAt.HasValue && x.EndAt.Value <= next24 && x.EndAt.Value > now);

            if (closingSoon > 0)
                alerts.Add(new DashboardAlertVM { Type = "warning", Message = $"يوجد {closingSoon} واجب ينتهي خلال 24 ساعة" });

            var liveExams = instructorExamAssignments.Count(x =>
            {
                if (!x.ScheduledDate.HasValue) return false;
                var end = CalculateExamEndAt(x.ScheduledDate, x.DurationMinutes);
                return x.ScheduledDate.Value <= now && end.HasValue && end.Value >= now;
            });

            if (liveExams > 0)
                alerts.Add(new DashboardAlertVM { Type = "danger", Message = $"{liveExams} اختبار جارٍ الآن" });

            return new InstructorDashboardViewModel
            {
                InstructorName = await GetInstructorNameAsync(instructorId),
                TotalStudents = totalStudents,
                TotalExamsSent = totalExamsSent,
                TotalHomeworks = totalHomeworks,
                ActiveHomeworksCount = activeHomeworks,
                OverallCompletionRate = completionRate,
                TotalBatches = batches.Count,
                AverageExamScore = avgExamScore,
                HomeworkCompletionRate = completionRate,
                RecentExams = recentExams,
                RecentHomeworks = recentHomeworks,
                Batches = batches,
                Alerts = alerts,
                AtRiskStudents = new(),
                BatchInsights = new()
            };
        }

        // ══════════════════════════════════════════════════
        // 🔧 Helper: بناء ViewModel صفحة تفاصيل الإحصاء
        // ══════════════════════════════════════════════════
        private InstructorStatDetailsViewModel BuildStatDetailsViewModel(
            string metric,
            InstructorDashboardViewModel dashboard)
        {
            var vm = new InstructorStatDetailsViewModel
            {
                Cards = new List<InstructorStatDetailsCardVM>
                {
                    new() { Label = "الطلاب",          Value = dashboard.TotalStudents.ToString(),      Tone = "success" },
                    new() { Label = "الدفعات",          Value = dashboard.Batches.Count.ToString(),      Tone = "info"    },
                    new() { Label = "الاختبارات",       Value = dashboard.TotalExamsSent.ToString(),     Tone = "accent"  },
                    new() { Label = "إتمام الواجبات",   Value = $"{dashboard.OverallCompletionRate}%",   Tone = "warning" }
                }
            };

            switch (metric)
            {
                case "exams":
                    vm.Title = "تحليل الاختبارات المرسلة";
                    vm.Subtitle = "كل الأرقام هنا مبنية على الدفعات النشطة المرتبطة بك فقط.";
                    vm.Value = dashboard.TotalExamsSent.ToString();
                    vm.Insight = "تابع الاختبارات الحديثة ومعدل التسليم قبل الانتقال لتقارير المراقبة التفصيلية.";
                    vm.ActionText = "عرض الاختبارات المرسلة";
                    vm.ActionUrl = Url.Action("SentExams", "InstructorExamSend", new { area = "Instructors" }) ?? "";
                    vm.Rows = dashboard.RecentExams.Select(x => new InstructorStatDetailsRowVM
                    {
                        Title = x.Title,
                        Meta = $"{x.TargetName} - {x.CreatedAt:yyyy/MM/dd}",
                        Value = x.TotalCount > 0 ? $"{x.SubmittedCount}/{x.TotalCount}" : "لا توجد محاولات",
                        Url = Url.Action("Report", "InstructorExamMonitoring", new { area = "Instructors", examAssignmentId = x.ExamAssignmentId }) ?? ""
                    }).ToList();
                    break;

                case "homeworks":
                    vm.Title = "تحليل الواجبات";
                    vm.Subtitle = "الواجبات الظاهرة تخص دفعاتك النشطة فقط ولا تشمل الدفعات المتخرجة.";
                    vm.Value = dashboard.TotalHomeworks.ToString();
                    vm.Insight = dashboard.ActiveHomeworksCount > 0
                        ? $"يوجد {dashboard.ActiveHomeworksCount} واجب مفتوح يحتاج متابعة."
                        : "لا توجد واجبات مفتوحة حاليا، ويمكن مراجعة التقارير المغلقة.";
                    vm.ActionText = "إدارة الواجبات";
                    vm.ActionUrl = Url.Action("Index", "InstructorHomeworks", new { area = "Instructors" }) ?? "";
                    vm.Rows = dashboard.RecentHomeworks.Select(x => new InstructorStatDetailsRowVM
                    {
                        Title = x.Title,
                        Meta = x.EndAt.HasValue ? $"ينتهي {x.EndAt:yyyy/MM/dd}" : "بدون موعد نهاية",
                        Value = $"{x.SubmittedCount}/{x.TotalStudents}",
                        Url = Url.Action("Students", "InstructorHomeworks", new { area = "Instructors", homeworkSetId = x.HomeworkSetId }) ?? ""
                    }).ToList();
                    break;

                case "completion":
                    vm.Title = "نسبة الإتمام الإجمالية";
                    vm.Subtitle = "مؤشر سريع لتفاعل الطلاب مع الواجبات داخل دفعاتك.";
                    vm.Value = $"{dashboard.OverallCompletionRate}%";
                    vm.Insight = dashboard.OverallCompletionRate >= 75
                        ? "المؤشر جيد، حافظ على نفس وتيرة المتابعة."
                        : "المؤشر يحتاج متابعة للطلاب الأقل التزاماً قبل تراكم الفجوة.";
                    vm.ActionText = "تقارير الواجبات";
                    vm.ActionUrl = Url.Action("Index", "InstructorHomeworks", new { area = "Instructors" }) ?? "";
                    vm.Rows = dashboard.RecentHomeworks.Select(x => new InstructorStatDetailsRowVM
                    {
                        Title = x.Title,
                        Meta = x.IsClosed ? "مغلق" : x.IsActive ? "نشط" : "منتهٍ",
                        Value = $"{x.CompletionPct}%",
                        Url = Url.Action("Students", "InstructorHomeworks", new { area = "Instructors", homeworkSetId = x.HomeworkSetId }) ?? ""
                    }).ToList();
                    break;

                default: // students
                    vm.Title = "طلاب دفعاتي";
                    vm.Subtitle = "عدد الطلاب الحاليين داخل الدفعات النشطة المرتبطة بك فقط.";
                    vm.Value = dashboard.TotalStudents.ToString();
                    vm.Insight = "عند تخريج دفعة لا تدخل في هذا الرقم ولا تظهر واجباتها أو اختباراتها أو تقاريرها.";
                    vm.ActionText = "عرض الحضور والانصراف";
                    vm.ActionUrl = Url.Action("Index", "InstructorAttendance", new { area = "Instructors" }) ?? "";
                    vm.Rows = dashboard.Batches.Select(x => new InstructorStatDetailsRowVM
                    {
                        Title = x.BatchName,
                        Meta = "دفعة نشطة مرتبطة بالمدرب",
                        Value = $"{x.StudentCount} طالب",
                        Url = Url.Action("Lectures", "InstructorAttendance", new { area = "Instructors", batchId = x.BatchId }) ?? ""
                    }).ToList();
                    break;
            }

            return vm;
        }

        // ══════════════════════════════════════════════════
        // 🔧 Scope Helpers (مفلترة بـ InstructorCurriculumBatches فقط)
        // ══════════════════════════════════════════════════

        // الدفعات المرتبطة مباشرة بالمدرب (منهج + دفعة)
        private async Task<List<int>> GetAllowedBatchIdsAsync(int instructorId)
        {
            var ids = await GetInstructorDirectBatchIdsAsync(instructorId);
            return ids == null ? new List<int>() : ids.Distinct().ToList();
        }

        // الطلاب المسجلون في الدفعات المباشرة للمدرب (مشتقة من الدفعات، بدون استدعاء مزدوج)
        private async Task<List<int>> GetStudentIdsForBatchesAsync(List<int> batchIds)
        {
            if (batchIds == null || batchIds.Count == 0)
                return new List<int>();

            return await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(x => batchIds.Contains(x.BatchId))
                .Select(x => x.StudentID)
                .Distinct()
                .ToListAsync();
        }

        private async Task<string> GetInstructorNameAsync(int instructorId)
        {
            var instructor = await _context.Instructors
                .AsNoTracking()
                .Where(x => x.Id == instructorId)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync();
            return instructor ?? "";
        }

        private static bool IsAllowedId(List<int> allowedIds, int id)
            => allowedIds != null && allowedIds.Any(x => x == id);

        // ══════════════════════════════════════════════════
        // 🔧 Date / Status Helpers
        // ══════════════════════════════════════════════════
        private static DateTime? CalculateExamEndAt(DateTime? scheduledDate, int durationMinutes)
        {
            if (!scheduledDate.HasValue) return null;
            return durationMinutes <= 0
                ? scheduledDate.Value
                : scheduledDate.Value.AddMinutes(durationMinutes);
        }

        private static bool IsHomeworkActive(DateTime? endAt, DateTime now)
            => endAt.HasValue && endAt.Value > now;

        private static bool IsHomeworkClosed(DateTime? endAt, DateTime now)
            => endAt.HasValue && endAt.Value <= now;

        private static int NormalizeTakeCount(int count)
            => count <= 0 ? 5 : count > 50 ? 50 : count;

        private static double ExtractScorePercent(string? note)
        {
            if (string.IsNullOrWhiteSpace(note)) return 0.0;
            try
            {
                var snapshot = JsonSerializer.Deserialize<QdratNew.DTOs.Exams.ExamFinalResultDto>(note);
                return snapshot?.ScorePercent ?? 0.0;
            }
            catch { return 0.0; }
        }

        // ══════════════════════════════════════════════════
        // 🔒 Internal DTOs
        // ══════════════════════════════════════════════════
        private sealed class DashboardExamApiItem
        {
            public int examAssignmentId { get; set; }
            public string title { get; set; } = "";
            public string examType { get; set; } = "";
            public string targetName { get; set; } = "";
            public DateTime? startAt { get; set; }
            public DateTime? endAt { get; set; }
            public int durationMinutes { get; set; }
            public DateTime createdAt { get; set; }
            public int submittedCount { get; set; }
            public int totalCount { get; set; }
        }

        private sealed class SectionPerformanceAccumulator
        {
            public int Correct { get; set; }
            public int Wrong { get; set; }
            public int Total { get; set; }
        }

        // ══════════════════════════════════════════════════
        // 🤖 تحليل الذكاء الاصطناعي - لوحة المدرب
        // ══════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> AIAnalysis()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            var batchIds = await GetInstructorDirectBatchIdsAsync(instructorId);
            if (!batchIds.Any())
                return View(new QdratNew.ViewModels.Instructor.InstructorAIPageViewModel
                {
                    InstructorName = CurrentInstructor?.FullName ?? ""
                });

            // ─── تحميل بيانات الدفعات ───────────────────────────
            var batches = await _context.Batches
                .AsNoTracking()
                .Where(b => batchIds.Contains(b.Id))
                .Select(b => new { b.Id, b.Name, CourseName = b.Course != null ? b.Course.Name : "" })
                .ToListAsync();

            // ─── ربط المدرب بالمناهج والدفعات ──────────────────
            var curriculumLinks = await _context.InstructorCurriculumBatches
                .AsNoTracking()
                .Where(icb => icb.InstructorId == instructorId && batchIds.Contains(icb.BatchId))
                .Select(icb => new { icb.BatchId, icb.CurriculumId })
                .ToListAsync();

            var allCurriculumIds = curriculumLinks.Select(c => c.CurriculumId).Distinct().ToList();

            // ─── الطلاب المسجلون ─────────────────────────────────
            var enrollments = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => batchIds.Contains(e.BatchId))
                .Select(e => new { e.BatchId, e.StudentID })
                .ToListAsync();

            var allStudentIds = enrollments.Select(e => e.StudentID).Distinct().ToList();

            var students = await _context.Students
                .AsNoTracking()
                .Where(s => allStudentIds.Contains(s.StudentID))
                .Select(s => new { s.StudentID, s.FullName })
                .ToListAsync();

            // ─── المحاضرات والحضور ───────────────────────────────
            var lectures = await _context.Lecture
                .AsNoTracking()
                .Where(l => batchIds.Contains(l.BatchId) && l.InstructorId == instructorId)
                .Select(l => new { l.Id, l.BatchId })
                .ToListAsync();

            var lectureIds = lectures.Select(l => l.Id).ToList();

            var attendanceRecords = await _context.AttendanceRecords
                .AsNoTracking()
                .Where(a => lectureIds.Contains(a.LectureId))
                .Select(a => new { a.LectureId, a.StudentId, a.IsPresent })
                .ToListAsync();

            // ─── الواجبات ────────────────────────────────────────
            var homeworkSets = await _context.HomeworkSets
                .AsNoTracking()
                .Where(h => batchIds.Contains(h.BatchId))
                .Select(h => new { h.Id, h.BatchId })
                .ToListAsync();

            var hwSetIds = homeworkSets.Select(h => h.Id).ToList();

            var homeworkStudents = await _context.HomeworkSetStudents
                .AsNoTracking()
                .Where(hs => hwSetIds.Contains(hs.HomeworkSetId))
                .Select(hs => new { hs.HomeworkSetId, hs.StudentId, hs.IsSubmitted })
                .ToListAsync();

            // ─── الاختبارات ──────────────────────────────────────
            var examAssignments = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Where(e => batchIds.Contains(e.BatchId))
                .Select(e => new { e.Id, e.BatchId })
                .ToListAsync();

            var examAssignmentIds = examAssignments.Select(e => e.Id).ToList();

            var examStatuses = await _context.ExamStudentStatuses
                .AsNoTracking()
                .Where(s => s.ExamAssignmentId.HasValue &&
                            examAssignmentIds.Contains(s.ExamAssignmentId.Value) &&
                            s.IsSubmitted)
                .Select(s => new { s.ExamAssignmentId, s.StudentId, s.Note })
                .ToListAsync();

            // ─── إتمام الدروس ─────────────────────────────────────
            var lessonCompletions = await _context.BatchLessonCompletions
                .AsNoTracking()
                .Where(lc => batchIds.Contains(lc.BatchId))
                .Select(lc => new { lc.BatchId, lc.LessonId })
                .ToListAsync();

            var totalLessonsByCurriculum = await _context.Lessons
                .AsNoTracking()
                .Where(l => allCurriculumIds.Contains(l.Section.CurriculumId))
                .GroupBy(l => l.Section.CurriculumId)
                .Select(g => new { CurriculumId = g.Key, Count = g.Count() })
                .ToListAsync();

            // ─── بناء تحليل كل دفعة ──────────────────────────────
            var teachingAnalyzer = new QdratNew.Services.AI.TeachingAIAnalyzer();
            var instructorAnalyzer = new QdratNew.Services.AI.InstructorAIAnalyzer();

            var batchInsights = new List<QdratNew.ViewModels.Instructor.BatchAIInsightVM>();
            float sumAttendance = 0, sumHomework = 0, sumExam = 0;

            foreach (var batch in batches)
            {
                var batchStudentIds = enrollments
                    .Where(e => e.BatchId == batch.Id)
                    .Select(e => e.StudentID)
                    .ToHashSet();

                // حضور الدفعة
                var batchLectureIds = lectures
                    .Where(l => l.BatchId == batch.Id)
                    .Select(l => l.Id)
                    .ToHashSet();

                var batchAttendance = attendanceRecords
                    .Where(a => batchLectureIds.Contains(a.LectureId))
                    .ToList();

                float avgAttendance = batchAttendance.Any()
                    ? (float)(batchAttendance.Count(a => a.IsPresent) * 100.0 / batchAttendance.Count)
                    : 0f;

                // إتمام الواجبات
                var batchHwIds = homeworkSets
                    .Where(h => h.BatchId == batch.Id)
                    .Select(h => h.Id)
                    .ToHashSet();

                var batchHwStudents = homeworkStudents
                    .Where(hs => batchHwIds.Contains(hs.HomeworkSetId) && batchStudentIds.Contains(hs.StudentId))
                    .ToList();

                float hwCompletionRate = batchHwStudents.Any()
                    ? (float)(batchHwStudents.Count(hs => hs.IsSubmitted) * 100.0 / batchHwStudents.Count)
                    : 0f;

                // درجات الاختبارات
                var batchExamIds = examAssignments
                    .Where(e => e.BatchId == batch.Id)
                    .Select(e => e.Id)
                    .ToHashSet();

                var batchExamStatuses = examStatuses
                    .Where(s => s.ExamAssignmentId.HasValue && batchExamIds.Contains(s.ExamAssignmentId.Value))
                    .ToList();

                var examScores = batchExamStatuses
                    .Select(s => ExtractScorePercent(s.Note))
                    .Where(s => s > 0)
                    .ToList();

                float avgExamScore = examScores.Any() ? (float)examScores.Average() : 0f;

                // إتمام الدروس
                var curriculumId = curriculumLinks.FirstOrDefault(c => c.BatchId == batch.Id)?.CurriculumId;
                int totalLessons = curriculumId.HasValue
                    ? totalLessonsByCurriculum.FirstOrDefault(l => l.CurriculumId == curriculumId.Value)?.Count ?? 0
                    : 0;
                int completedLessons = lessonCompletions.Count(lc => lc.BatchId == batch.Id);
                float lessonsRate = totalLessons > 0
                    ? (float)(completedLessons * 100.0 / totalLessons)
                    : 0f;

                // طلاب في خطر (متوسط أقل من 50)
                var studentScores = batchExamStatuses
                    .GroupBy(s => s.StudentId)
                    .Select(g => new
                    {
                        StudentId = g.Key,
                        AvgScore = (float)g.Select(s => ExtractScorePercent(s.Note)).Where(s => s > 0).DefaultIfEmpty(0).Average(),
                        ExamCount = g.Count()
                    })
                    .ToList();

                var atRiskStudents = studentScores
                    .Where(s => s.AvgScore < 50 && s.AvgScore > 0)
                    .Select(s => new QdratNew.ViewModels.Instructor.AtRiskStudentItemVM
                    {
                        StudentId = s.StudentId,
                        StudentName = students.FirstOrDefault(st => st.StudentID == s.StudentId)?.FullName ?? $"طالب #{s.StudentId}",
                        AvgScore = MathF.Round(s.AvgScore, 1),
                        BatchName = batch.Name,
                        ExamCount = s.ExamCount
                    })
                    .OrderBy(s => s.AvgScore)
                    .Take(5)
                    .ToList();

                // توصية الذكاء الاصطناعي للدفعة
                var batchInput = new QdratNew.MLModels.Instructors.BatchTeachingInsightInput
                {
                    AvgAttendance = avgAttendance,
                    AvgHomeworkScore = hwCompletionRate,
                    HomeworkCompletionRate = hwCompletionRate,
                    AvgTestScore = avgExamScore,
                    LessonsCompletionRate = lessonsRate,
                    StudentsAtRiskCount = atRiskStudents.Count
                };

                var aiRecommendation = teachingAnalyzer.Analyze(batchInput);

                // مستوى الأداء
                string perfLevel, perfTone;
                if (avgExamScore >= 85) { perfLevel = "ممتاز"; perfTone = "success"; }
                else if (avgExamScore >= 70) { perfLevel = "جيد جداً"; perfTone = "info"; }
                else if (avgExamScore >= 50) { perfLevel = "جيد"; perfTone = "warning"; }
                else { perfLevel = "يحتاج متابعة"; perfTone = "danger"; }

                batchInsights.Add(new QdratNew.ViewModels.Instructor.BatchAIInsightVM
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    CourseName = batch.CourseName,
                    StudentCount = batchStudentIds.Count,
                    AvgAttendance = MathF.Round(avgAttendance, 1),
                    HomeworkCompletionRate = MathF.Round(hwCompletionRate, 1),
                    AvgExamScore = MathF.Round(avgExamScore, 1),
                    LessonsCompletionRate = MathF.Round(lessonsRate, 1),
                    AtRiskCount = atRiskStudents.Count,
                    AIRecommendation = aiRecommendation,
                    PerformanceLevel = perfLevel,
                    PerformanceTone = perfTone,
                    AtRiskStudents = atRiskStudents
                });

                sumAttendance += avgAttendance;
                sumHomework += hwCompletionRate;
                sumExam += avgExamScore;
            }

            int batchCount = batchInsights.Count;
            float overallAttendance = batchCount > 0 ? MathF.Round(sumAttendance / batchCount, 1) : 0f;
            float overallHomework = batchCount > 0 ? MathF.Round(sumHomework / batchCount, 1) : 0f;
            float overallExam = batchCount > 0 ? MathF.Round(sumExam / batchCount, 1) : 0f;
            float overallLessons = batchInsights.Any()
                ? MathF.Round((float)batchInsights.Average(b => b.LessonsCompletionRate), 1)
                : 0f;

            // التوصية الشاملة من النموذج
            var overallAI = instructorAnalyzer.GetRecommendation(
                overallAttendance, overallHomework, overallLessons, overallExam);

            // التوصية المبنية على القواعد
            var passCount = batchInsights.Sum(b =>
                (int)Math.Round(b.AvgExamScore >= 50 ? b.StudentCount * 0.8 : b.StudentCount * 0.3));
            var totalStu = batchInsights.Sum(b => b.StudentCount);
            var ruleVM = new QdratNew.ViewModels.Instructor.InstructorPerformanceAIViewModel
            {
                InstructorId = instructorId,
                InstructorName = CurrentInstructor?.FullName ?? "",
                TotalCurriculums = allCurriculumIds.Count,
                AverageStudentScore = overallExam,
                SuccessRate = totalStu > 0 ? Math.Round(passCount * 100.0 / totalStu, 1) : 0
            };
            var ruleRec = QdratNew.Services.AI.InstructorPerformanceAdvisor.GetRecommendation(ruleVM);

            var vm = new QdratNew.ViewModels.Instructor.InstructorAIPageViewModel
            {
                InstructorName = CurrentInstructor?.FullName ?? "",
                OverallAIRecommendation = overallAI,
                RuleBasedRecommendation = ruleRec,
                TotalStudents = enrollments.Select(e => e.StudentID).Distinct().Count(),
                TotalBatches = batchCount,
                AvgAttendanceRate = overallAttendance,
                AvgHomeworkCompletionRate = overallHomework,
                AvgExamScore = overallExam,
                TotalAtRiskStudents = batchInsights.Sum(b => b.AtRiskCount),
                BatchInsights = batchInsights,
                GlobalAtRiskStudents = batchInsights
                    .SelectMany(b => b.AtRiskStudents)
                    .OrderBy(s => s.AvgScore)
                    .Take(10)
                    .ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> TrainModels()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();
            return View();
        }



    }
}
