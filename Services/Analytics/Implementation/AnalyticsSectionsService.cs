using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Admin.Analytics;
using QdratNew.ViewModels.Analytics;

namespace QdratNew.Services.Analytics.Implementation
{
    /// <summary>
    /// Replaces in-memory full-table scans with SQL-level GROUP BY aggregations.
    /// The application never loads raw attempt rows — only the computed totals.
    /// </summary>
    public class AnalyticsSectionsService : IAnalyticsSectionsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBatchDecisionStatusService _batchDecisionService;
        private readonly IMemoryCache _cache;

        private const string CacheTokenKey = "analytics_sec_cts";
        private const int PageSize = 10;

        public AnalyticsSectionsService(
            ApplicationDbContext context,
            IBatchDecisionStatusService batchDecisionService,
            IMemoryCache cache)
        {
            _context = context;
            _batchDecisionService = batchDecisionService;
            _cache = cache;
        }

        // ──────────────────────────────────────────────────────────────────
        // SHELL  — filters only, instant response
        // ──────────────────────────────────────────────────────────────────

        public async Task<AnalyticsShellVM> GetShellAsync(
            int? curriculumId, int? batchId, int? instructorId)
        {
            var curriculums = await _context.Curriculums
                .AsNoTracking()
                .OrderBy(x => x.Title)
                .Select(x => new AnalyticsFilterOptionVM { Id = x.Id, Name = x.Title })
                .ToListAsync();

            var batches = await _context.Set<Batch>()
                .AsNoTracking()
                .Where(b => b.IsActive && !b.IsDeleted)
                .OrderBy(b => b.Name)
                .Select(b => new AnalyticsFilterOptionVM { Id = b.Id, Name = b.Name })
                .ToListAsync();

            var activeInstructorIds = await GetActiveInstructorIdsAsync();

            var instructors = await _context.Set<Instructor>()
                .AsNoTracking()
                .Where(i => activeInstructorIds.Contains(i.Id))
                .OrderBy(i => i.FullName)
                .Select(i => new AnalyticsFilterOptionVM { Id = i.Id, Name = i.FullName })
                .ToListAsync();

            return new AnalyticsShellVM
            {
                SelectedCurriculumId = curriculumId,
                SelectedBatchId      = batchId,
                SelectedInstructorId = instructorId,
                CurriculumsFilter    = curriculums,
                BatchesFilter        = batches,
                InstructorsFilter    = instructors
            };
        }

        // ──────────────────────────────────────────────────────────────────
        // FULL DASHBOARD  — all heavy work done at SQL level
        // ──────────────────────────────────────────────────────────────────

        public async Task<AdvancedAnalyticsDashboardVM> BuildAsync(
            int? curriculumId, int? batchId, int? instructorId, int page)
        {
            if (page < 1) page = 1;

            var cacheKey = $"analytics_sec_{curriculumId}_{batchId}_{instructorId}_{page}";
            if (_cache.TryGetValue(cacheKey, out AdvancedAnalyticsDashboardVM? cached) && cached != null)
                return cached;

            // IQueryable — not executed until each .ToListAsync() below
            var attemptsQ = BuildFilteredQuery(curriculumId, batchId, instructorId);

            // ═══════════════════════════════════════════════════════════════
            // § A  STUDENT SCORE AGGREGATION  (SQL GROUP BY → 1 row/student)
            // ═══════════════════════════════════════════════════════════════
            var studentAgg = await attemptsQ
                .GroupBy(x => x.StudentId)
                .Select(g => new StudentAgg
                {
                    StudentId       = g.Key,
                    TotalExam       = g.Count(x => x.ExamAssignmentId != null
                                               || x.ExamId != null
                                               || x.PerformanceIndicatorExamId != null),
                    CorrectExam     = g.Count(x => (x.ExamAssignmentId != null
                                               || x.ExamId != null
                                               || x.PerformanceIndicatorExamId != null)
                                               && x.IsCorrect),
                    TotalHomework   = g.Count(x => x.HomeworkSetId != null),
                    CorrectHomework = g.Count(x => x.HomeworkSetId != null && x.IsCorrect),
                })
                .ToListAsync();

            var studentIds = studentAgg.Select(x => x.StudentId).ToList();

            // ═══════════════════════════════════════════════════════════════
            // § B  LESSON AGGREGATION  (SQL GROUP BY → 1 row/lesson)
            // ═══════════════════════════════════════════════════════════════
            var lessonAgg = await attemptsQ
                .Where(x => x.LessonId != null)
                .GroupBy(x => x.LessonId)
                .Select(g => new LessonAgg
                {
                    LessonId      = g.Key!.Value,
                    TotalAttempts = g.Count(),
                    WrongAttempts = g.Count(x => !x.IsCorrect),
                })
                .ToListAsync();

            // ═══════════════════════════════════════════════════════════════
            // § C  BATCH AGGREGATION  (SQL JOIN + GROUP BY → 1 row/batch)
            // ═══════════════════════════════════════════════════════════════
            var batchAgg = await _context.Set<StudentBatchEnrollment>()
                .AsNoTracking()
                .Join(attemptsQ,
                      e => e.StudentID, a => a.StudentId,
                      (e, a) => new { e.BatchId, a.IsCorrect })
                .GroupBy(x => x.BatchId)
                .Select(g => new BatchAgg
                {
                    BatchId       = g.Key,
                    TotalAttempts = g.Count(),
                    WrongAttempts = g.Count(x => !x.IsCorrect),
                })
                .ToListAsync();

            // ═══════════════════════════════════════════════════════════════
            // § D  ATTENDANCE AGGREGATION  (SQL GROUP BY → 1 row/student)
            // ═══════════════════════════════════════════════════════════════
            var attendanceAgg = studentIds.Count > 0
                ? await _context.AttendanceRecords
                    .AsNoTracking()
                    .Where(a => studentIds.Contains(a.StudentId))
                    .GroupBy(a => a.StudentId)
                    .Select(g => new AttAgg
                    {
                        StudentId = g.Key,
                        Total     = g.Count(),
                        Present   = g.Count(a => a.IsPresent)
                    })
                    .ToListAsync()
                : new List<AttAgg>();

            var attendanceMap = attendanceAgg.ToDictionary(x => x.StudentId);

            // ═══════════════════════════════════════════════════════════════
            // § E  LESSON AFFECTED STUDENTS  (SQL DISTINCT + GROUP BY)
            // ═══════════════════════════════════════════════════════════════
            var weakLessonIds = lessonAgg
                .Where(l => l.TotalAttempts > 0
                         && l.WrongAttempts * 100.0 / l.TotalAttempts >= 50)
                .Select(l => l.LessonId)
                .ToList();

            var lessonAffectedMap = weakLessonIds.Count > 0
                ? await attemptsQ
                    .Where(x => x.LessonId != null
                             && !x.IsCorrect
                             && weakLessonIds.Contains(x.LessonId!.Value))
                    .Select(x => new { LessonId = x.LessonId!.Value, x.StudentId })
                    .Distinct()
                    .GroupBy(x => x.LessonId)
                    .Select(g => new { LessonId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.LessonId, x => x.Count)
                : new Dictionary<int, int>();

            // ═══════════════════════════════════════════════════════════════
            // § F  INSTRUCTOR AGGREGATION  (3 JOIN chains → 1 row/instructor)
            // ═══════════════════════════════════════════════════════════════
            var icbAgg = await _context.Set<InstructorCurriculumBatch>()
                .AsNoTracking()
                .Join(_context.Set<StudentBatchEnrollment>(),
                      icb => icb.BatchId, e => e.BatchId,
                      (icb, e) => new { icb.InstructorId, e.StudentID })
                .Join(attemptsQ,
                      x => x.StudentID, a => a.StudentId,
                      (x, a) => new { x.InstructorId, a.IsCorrect })
                .GroupBy(x => x.InstructorId)
                .Select(g => new InstrAgg
                {
                    InstructorId    = g.Key,
                    TotalAttempts   = g.Count(),
                    CorrectAttempts = g.Count(x => x.IsCorrect),
                })
                .ToListAsync();

            var ciAgg = await _context.Set<CourseInstructor>()
                .AsNoTracking()
                .Join(_context.Set<Batch>(),
                      ci => ci.CourseID, b => b.CourseId,
                      (ci, b) => new { ci.InstructorID, BatchId = b.Id })
                .Join(_context.Set<StudentBatchEnrollment>(),
                      x => x.BatchId, e => e.BatchId,
                      (x, e) => new { InstructorId = x.InstructorID, e.StudentID })
                .Join(attemptsQ,
                      x => x.StudentID, a => a.StudentId,
                      (x, a) => new { x.InstructorId, a.IsCorrect })
                .GroupBy(x => x.InstructorId)
                .Select(g => new InstrAgg
                {
                    InstructorId    = g.Key,
                    TotalAttempts   = g.Count(),
                    CorrectAttempts = g.Count(x => x.IsCorrect),
                })
                .ToListAsync();

            var examAgg = await _context.Set<ExamAssignmentToBatch>()
                .AsNoTracking()
                .Where(x => x.CreatedByInstructorId != null)
                .Join(_context.Set<StudentBatchEnrollment>(),
                      x => x.BatchId, e => e.BatchId,
                      (x, e) => new { InstructorId = x.CreatedByInstructorId!.Value, e.StudentID })
                .Join(attemptsQ,
                      x => x.StudentID, a => a.StudentId,
                      (x, a) => new { x.InstructorId, a.IsCorrect })
                .GroupBy(x => x.InstructorId)
                .Select(g => new InstrAgg
                {
                    InstructorId    = g.Key,
                    TotalAttempts   = g.Count(),
                    CorrectAttempts = g.Count(x => x.IsCorrect),
                })
                .ToListAsync();

            // Batch count per instructor
            var instrBatchCounts = await _context.Set<InstructorCurriculumBatch>()
                .AsNoTracking()
                .GroupBy(x => x.InstructorId)
                .Select(g => new { InstructorId = g.Key, Count = g.Select(x => x.BatchId).Distinct().Count() })
                .ToDictionaryAsync(x => x.InstructorId, x => x.Count);

            // ═══════════════════════════════════════════════════════════════
            // § G  REFERENCE DATA  — only for matched IDs (small payloads)
            // ═══════════════════════════════════════════════════════════════
            var studentNameMap = studentIds.Count > 0
                ? await _context.Students.AsNoTracking()
                    .Where(s => studentIds.Contains(s.StudentID))
                    .Select(s => new { s.StudentID, s.FullName })
                    .ToDictionaryAsync(s => s.StudentID, s => s.FullName)
                : new Dictionary<int, string>();

            var lessonNameMap = weakLessonIds.Count > 0
                ? await _context.Set<Lesson>().AsNoTracking()
                    .Where(l => weakLessonIds.Contains(l.Id))
                    .Select(l => new { l.Id, l.Title })
                    .ToDictionaryAsync(l => l.Id, l => l.Title)
                : new Dictionary<int, string>();

            var batchesDb = await _context.Set<Batch>().AsNoTracking()
                .Where(b => b.IsActive && !b.IsDeleted)
                .Select(b => new { b.Id, b.Name, b.CourseId })
                .ToListAsync();

            var courseIds = batchesDb.Select(b => b.CourseId).Distinct().ToList();
            var courseNameMap = await _context.Set<Course>().AsNoTracking()
                .Where(c => courseIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToDictionaryAsync(c => c.Id, c => c.Name);

            var studentBatchMap = studentIds.Count > 0
                ? await _context.Set<StudentBatchEnrollment>().AsNoTracking()
                    .Where(e => studentIds.Contains(e.StudentID))
                    .GroupBy(e => e.StudentID)
                    .Select(g => new { StudentId = g.Key, BatchId = g.Min(e => e.BatchId) })
                    .ToDictionaryAsync(x => x.StudentId, x => x.BatchId)
                : new Dictionary<int, int>();

            var instructorNameMap = await _context.Set<Instructor>().AsNoTracking()
                .Select(i => new { i.Id, i.FullName })
                .ToDictionaryAsync(i => i.Id, i => i.FullName);

            // Filter dropdowns
            var curriculumsFilter = await _context.Curriculums.AsNoTracking()
                .OrderBy(x => x.Title)
                .Select(x => new AnalyticsFilterOptionVM { Id = x.Id, Name = x.Title })
                .ToListAsync();

            var batchesFilter = batchesDb
                .OrderBy(b => b.Name)
                .Select(b => new AnalyticsFilterOptionVM { Id = b.Id, Name = b.Name })
                .ToList();

            var activeInstructorIds = await GetActiveInstructorIdsAsync();
            var instructorsFilter = await _context.Set<Instructor>().AsNoTracking()
                .Where(i => activeInstructorIds.Contains(i.Id))
                .OrderBy(i => i.FullName)
                .Select(i => new AnalyticsFilterOptionVM { Id = i.Id, Name = i.FullName })
                .ToListAsync();

            // ═══════════════════════════════════════════════════════════════
            // § H  BUILD STUDENT ANALYTICS  (tiny in-memory loop)
            // ═══════════════════════════════════════════════════════════════
            var students = studentAgg
                .Select(s =>
                {
                    var examScore     = s.TotalExam     == 0 ? 0.0 : s.CorrectExam     * 100.0 / s.TotalExam;
                    var homeworkScore = s.TotalHomework == 0 ? 0.0 : s.CorrectHomework * 100.0 / s.TotalHomework;
                    var attRate       = !attendanceMap.TryGetValue(s.StudentId, out var att) || att.Total == 0
                                        ? 0.0 : att.Present * 100.0 / att.Total;

                    string weakReason, action, priority;
                    if (examScore < 50 && homeworkScore < 50)
                        (weakReason, action, priority) = ("ضعف عام في الفهم", "إعادة شرح + واجبات إضافية + اختبار قصير", "High");
                    else if (examScore < 50)
                        (weakReason, action, priority) = ("ضعف في الاختبارات", "تدريب على نماذج امتحانات", "High");
                    else if (homeworkScore < 50)
                        (weakReason, action, priority) = ("عدم التزام بالواجبات", "متابعة يومية + واجب إجباري", "Medium");
                    else if (attRate < 50)
                        (weakReason, action, priority) = ("غياب متكرر", "تنبيه ولي الأمر", "High");
                    else
                        (weakReason, action, priority) = ("أداء متوسط", "متابعة فقط", "Low");

                    studentBatchMap.TryGetValue(s.StudentId, out var bId);

                    return new StudentAnalyticsVM
                    {
                        StudentId      = s.StudentId,
                        StudentName    = studentNameMap.GetValueOrDefault(s.StudentId, "غير معروف"),
                        ExamScore      = Math.Round(examScore,     1),
                        HomeworkScore  = Math.Round(homeworkScore, 1),
                        Attendance     = Math.Round(attRate,       1),
                        WeakReason     = weakReason,
                        ActionRequired = action,
                        Priority       = priority,
                        BatchId        = bId > 0 ? bId : null
                    };
                })
                .Where(x => x.ExamScore < 60 || x.HomeworkScore < 60 || x.Attendance < 60)
                .OrderBy(x => x.ExamScore)
                .ThenBy(x => x.HomeworkScore)
                .Take(50)
                .ToList();

            // ═══════════════════════════════════════════════════════════════
            // § I  BUILD LESSON ANALYTICS
            // ═══════════════════════════════════════════════════════════════
            var lessons = lessonAgg
                .Where(l => l.TotalAttempts > 0 && l.WrongAttempts * 100.0 / l.TotalAttempts >= 50)
                .Select(l =>
                {
                    var weakPct = l.WrongAttempts * 100.0 / l.TotalAttempts;
                    lessonAffectedMap.TryGetValue(l.LessonId, out var affected);
                    return new LessonAnalyticsVM
                    {
                        LessonId         = l.LessonId,
                        LessonName       = lessonNameMap.GetValueOrDefault(l.LessonId, "درس غير معروف"),
                        TotalAttempts    = l.TotalAttempts,
                        AffectedStudents = affected,
                        StudentsCount    = affected,
                        WeakPercentage   = Math.Round(weakPct, 1),
                        BatchName        = "-",
                        InstructorName   = "-"
                    };
                })
                .OrderByDescending(x => x.WeakPercentage)
                .ThenByDescending(x => x.AffectedStudents)
                .Take(20)
                .ToList();

            // ═══════════════════════════════════════════════════════════════
            // § J  BUILD BATCH ANALYTICS  + PAGINATION
            // ═══════════════════════════════════════════════════════════════
            var batchAggMap = batchAgg.ToDictionary(b => b.BatchId);

            var batchAnalytics = batchesDb
                .Select(b =>
                {
                    batchAggMap.TryGetValue(b.Id, out var raw);
                    var total      = raw?.TotalAttempts ?? 0;
                    var wrong      = raw?.WrongAttempts ?? 0;
                    var avgWeak    = total == 0 ? 0.0 : wrong * 100.0 / total;
                    courseNameMap.TryGetValue(b.CourseId, out var courseName);

                    return new BatchAnalyticsVM
                    {
                        BatchId              = b.Id,
                        BatchName            = b.Name,
                        CourseName           = courseName ?? "-",
                        AvgWeakness          = Math.Round(avgWeak, 1),
                        RiskLevel            = avgWeak >= 70 ? "High" : avgWeak >= 50 ? "Medium" : "Low",
                        StudentsCount        = 0,
                        WeakLessonsCount     = 0,
                        CriticalLessonsCount = 0,
                        AffectedStudents     = 0
                    };
                })
                .Where(x => x.AvgWeakness > 0)
                .OrderByDescending(x => x.AvgWeakness)
                .ToList();

            var totalPages  = batchAnalytics.Count == 0
                ? 1 : (int)Math.Ceiling(batchAnalytics.Count / (double)PageSize);
            if (page > totalPages) page = totalPages;

            var pagedBatches      = batchAnalytics.Skip((page - 1) * PageSize).Take(PageSize).ToList();
            var decisionStatusMap = await _batchDecisionService
                .GetStatusForBatchesAsync(pagedBatches.Select(b => b.BatchId));

            // ═══════════════════════════════════════════════════════════════
            // § K  BUILD INSTRUCTOR ANALYTICS
            // ═══════════════════════════════════════════════════════════════
            var allInstrIds = icbAgg.Select(x => x.InstructorId)
                .Union(ciAgg .Select(x => x.InstructorId))
                .Union(examAgg.Select(x => x.InstructorId))
                .Distinct().ToList();

            var instructorAnalytics = allInstrIds
                .Select(id =>
                {
                    var a = icbAgg .FirstOrDefault(x => x.InstructorId == id);
                    var b = ciAgg  .FirstOrDefault(x => x.InstructorId == id);
                    var c = examAgg.FirstOrDefault(x => x.InstructorId == id);

                    var totalAll   = (a?.TotalAttempts ?? 0) + (b?.TotalAttempts ?? 0) + (c?.TotalAttempts ?? 0);
                    var correctAll = (a?.CorrectAttempts ?? 0) + (b?.CorrectAttempts ?? 0) + (c?.CorrectAttempts ?? 0);
                    var avgScore   = totalAll == 0 ? 0.0 : correctAll * 100.0 / totalAll;
                    instrBatchCounts.TryGetValue(id, out var bc);

                    return new InstructorAnalyticsVM
                    {
                        InstructorId     = id,
                        InstructorName   = instructorNameMap.GetValueOrDefault(id, "مدرب غير معروف"),
                        AvgScore         = Math.Round(avgScore, 1),
                        WeakLessonsCount = 0,
                        BatchesCount     = bc,
                        RiskLevel        = avgScore < 50 ? "High" : avgScore < 70 ? "Medium" : "Low"
                    };
                })
                .Where(x => x.BatchesCount > 0)
                .OrderBy(x => x.AvgScore)
                .ThenByDescending(x => x.WeakLessonsCount)
                .ToList();

            // ═══════════════════════════════════════════════════════════════
            // § L  KPIs  — from already-small aggregated lists
            // ═══════════════════════════════════════════════════════════════
            var avgExam       = students.Any() ? Math.Round(students.Average(x => x.ExamScore),     1) : 0.0;
            var avgHomework   = students.Any() ? Math.Round(students.Average(x => x.HomeworkScore), 1) : 0.0;
            var avgAttendance = students.Any() ? Math.Round(students.Average(x => x.Attendance),    1) : 0.0;

            var lessonWeaknessValues = lessonAgg
                .Where(l => l.TotalAttempts > 0)
                .Select(l => l.WrongAttempts * 100.0 / l.TotalAttempts)
                .ToList();
            var avgWeaknessKpi = lessonWeaknessValues.Any()
                ? Math.Round(lessonWeaknessValues.Average(), 1) : 0.0;

            // ═══════════════════════════════════════════════════════════════
            // § M  ASSEMBLE VM
            // ═══════════════════════════════════════════════════════════════
            var vm = new AdvancedAnalyticsDashboardVM
            {
                Students          = students,
                Lessons           = lessons,
                Batches           = pagedBatches,
                Instructors       = instructorAnalytics,

                WeakStudents      = students.Count,
                WeakStudentsCount = students.Count(x => x.ExamScore < 60),

                AvgExamScore     = avgExam,
                AvgHomeworkScore = avgHomework,
                AvgAttendance    = avgAttendance,
                CriticalCases    = students.Count(x => x.Priority == "High"),

                TotalBatches     = batchesDb.Count,
                TotalInstructors = instructorAnalytics.Count,
                HighRiskCount    = lessons.Count(x => x.WeakPercentage >= 70),
                AvgWeakness      = avgWeaknessKpi,

                RiskHigh   = batchAnalytics.Count(x => x.AvgWeakness >= 70),
                RiskMedium = batchAnalytics.Count(x => x.AvgWeakness >= 50 && x.AvgWeakness < 70),
                RiskLow    = batchAnalytics.Count(x => x.AvgWeakness < 50),

                ExcellentStudents = students.Count(x => x.ExamScore >= 80),
                MediumStudents    = students.Count(x => x.ExamScore >= 60 && x.ExamScore < 80),

                InstructorNames  = instructorAnalytics.Select(x => x.InstructorName).ToList(),
                InstructorScores = instructorAnalytics.Select(x => x.AvgScore).ToList(),

                CurrentPage = page,
                TotalPages  = totalPages,

                SelectedCurriculumId = curriculumId,
                SelectedBatchId      = batchId,
                SelectedInstructorId = instructorId,

                CurriculumsFilter = curriculumsFilter,
                BatchesFilter     = batchesFilter,
                InstructorsFilter = instructorsFilter,

                DecisionStatusMap = decisionStatusMap
            };

            _cache.Set(cacheKey, vm,
                new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .AddExpirationToken(new CancellationChangeToken(GetCacheToken())));

            return vm;
        }

        // ──────────────────────────────────────────────────────────────────
        // HELPERS
        // ──────────────────────────────────────────────────────────────────

        private IQueryable<QuestionAttemptNew> BuildFilteredQuery(
            int? curriculumId, int? batchId, int? instructorId)
        {
            IQueryable<QuestionAttemptNew> q = _context.QuestionAttemptNew.AsNoTracking();

            if (batchId.HasValue)
            {
                var ids = _context.Set<StudentBatchEnrollment>()
                    .Where(e => e.BatchId == batchId).Select(e => e.StudentID);
                q = q.Where(x => ids.Contains(x.StudentId));
            }

            if (curriculumId.HasValue)
            {
                var sectionIds = _context.Set<Section>()
                    .Where(s => s.CurriculumId == curriculumId).Select(s => s.Id);
                var lessonIds = _context.Set<Lesson>()
                    .Where(l => sectionIds.Contains(l.SectionId)).Select(l => l.Id);

                q = q.Where(x =>
                    (x.LessonId.HasValue  && lessonIds .Contains(x.LessonId .Value)) ||
                    (x.SectionId.HasValue && sectionIds.Contains(x.SectionId.Value)));
            }

            if (instructorId.HasValue)
            {
                var bids = BuildInstructorBatchIds(instructorId.Value);
                var sids = _context.Set<StudentBatchEnrollment>()
                    .Where(e => bids.Contains(e.BatchId)).Select(e => e.StudentID);
                q = q.Where(x => sids.Contains(x.StudentId));
            }

            return q;
        }

        private IQueryable<int> BuildInstructorBatchIds(int instructorId)
        {
            var fromIcb = _context.Set<InstructorCurriculumBatch>()
                .Where(x => x.InstructorId == instructorId).Select(x => x.BatchId);

            var courseIds = _context.Set<CourseInstructor>()
                .Where(x => x.InstructorID == instructorId).Select(x => x.CourseID);
            var fromCourses = _context.Set<Batch>()
                .Where(b => courseIds.Contains(b.CourseId)).Select(b => b.Id);

            var fromExams = _context.Set<ExamAssignmentToBatch>()
                .Where(x => x.CreatedByInstructorId == instructorId).Select(x => x.BatchId);

            return fromIcb.Union(fromCourses).Union(fromExams);
        }

        private async Task<List<int>> GetActiveInstructorIdsAsync()
            => await _context.Set<InstructorCurriculumBatch>()
                .Select(x => x.InstructorId)
                .Union(_context.Set<CourseInstructor>().Select(x => x.InstructorID))
                .Union(_context.Set<ExamAssignmentToBatch>()
                    .Where(x => x.CreatedByInstructorId != null)
                    .Select(x => x.CreatedByInstructorId!.Value))
                .Distinct()
                .ToListAsync();

        // ──────────────────────────────────────────────────────────────────
        // CACHE TOKEN
        // ──────────────────────────────────────────────────────────────────

        private CancellationToken GetCacheToken()
        {
            if (!_cache.TryGetValue(CacheTokenKey, out CancellationTokenSource? cts)
                || cts == null || cts.IsCancellationRequested)
            {
                cts = new CancellationTokenSource();
                _cache.Set(CacheTokenKey, cts,
                    new MemoryCacheEntryOptions().SetPriority(CacheItemPriority.NeverRemove));
            }
            return cts.Token;
        }

        public void InvalidateCache()
        {
            if (_cache.TryGetValue(CacheTokenKey, out CancellationTokenSource? cts) && cts != null)
            { cts.Cancel(); cts.Dispose(); }
            _cache.Remove(CacheTokenKey);
        }

        // ──────────────────────────────────────────────────────────────────
        // PRIVATE PROJECTION RECORDS (avoid anonymous-type reflection)
        // ──────────────────────────────────────────────────────────────────

        private sealed class StudentAgg
        {
            public int StudentId       { get; set; }
            public int TotalExam       { get; set; }
            public int CorrectExam     { get; set; }
            public int TotalHomework   { get; set; }
            public int CorrectHomework { get; set; }
        }

        private sealed class LessonAgg
        {
            public int LessonId      { get; set; }
            public int TotalAttempts { get; set; }
            public int WrongAttempts { get; set; }
        }

        private sealed class BatchAgg
        {
            public int BatchId       { get; set; }
            public int TotalAttempts { get; set; }
            public int WrongAttempts { get; set; }
        }

        private sealed class AttAgg
        {
            public int StudentId { get; set; }
            public int Total     { get; set; }
            public int Present   { get; set; }
        }

        private sealed class InstrAgg
        {
            public int InstructorId    { get; set; }
            public int TotalAttempts   { get; set; }
            public int CorrectAttempts { get; set; }
        }
    }
}
