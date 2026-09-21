using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Admin.Analytics;
using QdratNew.ViewModels.Analytics;

namespace QdratNew.Services.Analytics
{
    public class AnalyticsDashboardService : IAnalyticsDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBatchDecisionStatusService _batchDecisionStatusService;
        private readonly IMemoryCache _cache;

        private const string CacheTokenKey = "analytics_cache_cts";
        private const int PageSize = 10;

        public AnalyticsDashboardService(
            ApplicationDbContext context,
            IBatchDecisionStatusService batchDecisionStatusService,
            IMemoryCache cache)
        {
            _context = context;
            _batchDecisionStatusService = batchDecisionStatusService;
            _cache = cache;
        }

        public async Task<AdvancedAnalyticsDashboardVM> BuildDashboardAsync(
            int? curriculumId, int? batchId, int? instructorId, int page)
        {
            if (page < 1) page = 1;

            var cacheKey = $"analytics_dashboard_{curriculumId}_{batchId}_{instructorId}_{page}";

            if (_cache.TryGetValue(cacheKey, out AdvancedAnalyticsDashboardVM? cached) && cached != null)
                return cached;

            // ── 1. بناء استعلام المحاولات بفلاتر SQL مباشرة ──────────────────────
            IQueryable<QuestionAttemptNew> attemptsQuery = _context.QuestionAttemptNew.AsNoTracking();

            if (batchId.HasValue)
            {
                var batchStudentIds = _context.Set<StudentBatchEnrollment>()
                    .Where(e => e.BatchId == batchId)
                    .Select(e => e.StudentID);

                attemptsQuery = attemptsQuery.Where(x => batchStudentIds.Contains(x.StudentId));
            }

            if (curriculumId.HasValue)
            {
                var sectionIds = _context.Set<Section>()
                    .Where(s => s.CurriculumId == curriculumId)
                    .Select(s => s.Id);

                var lessonIds = _context.Set<Lesson>()
                    .Where(l => sectionIds.Contains(l.SectionId))
                    .Select(l => l.Id);

                attemptsQuery = attemptsQuery.Where(x =>
                    (x.LessonId.HasValue && lessonIds.Contains(x.LessonId.Value)) ||
                    (x.SectionId.HasValue && sectionIds.Contains(x.SectionId.Value)));
            }

            if (instructorId.HasValue)
            {
                var instructorBatchIds = BuildInstructorBatchIdsQuery(instructorId.Value);

                var instructorStudentIds = _context.Set<StudentBatchEnrollment>()
                    .Where(e => instructorBatchIds.Contains(e.BatchId))
                    .Select(e => e.StudentID);

                attemptsQuery = attemptsQuery.Where(x => instructorStudentIds.Contains(x.StudentId));
            }

            var examAttempts = await attemptsQuery.ToListAsync();

            // ── 2. تحميل البيانات المساعدة بحجم محدود ────────────────────────────
            var affectedStudentIds = examAttempts.Select(x => x.StudentId).Distinct().ToList();

            var studentsDb = await _context.Students
                .AsNoTracking()
                .ToListAsync();

            var attendanceRecords = affectedStudentIds.Count > 0
                ? await _context.AttendanceRecords
                    .AsNoTracking()
                    .Where(a => affectedStudentIds.Contains(a.StudentId))
                    .ToListAsync()
                : new List<AttendanceRecord>();

            var lessonsDb = await _context.Set<Lesson>().AsNoTracking().ToListAsync();
            var sectionsDb = await _context.Set<Section>().AsNoTracking().ToListAsync();
            var batchesDb = await _context.Set<Batch>().AsNoTracking().ToListAsync();
            var coursesDb = await _context.Set<Course>().AsNoTracking().ToListAsync();
            var instructorsDb = await _context.Set<Instructor>().AsNoTracking().ToListAsync();
            var courseInstructorsDb = await _context.Set<CourseInstructor>().AsNoTracking().ToListAsync();
            var instructorCurriculumBatchesDb = await _context.Set<InstructorCurriculumBatch>().AsNoTracking().ToListAsync();
            var studentBatchEnrollments = await _context.Set<StudentBatchEnrollment>().AsNoTracking().ToListAsync();
            var examAssignments = await _context.Set<ExamAssignmentToBatch>().AsNoTracking().ToListAsync();

            // ── 3. بناء فهارس للبحث O(1) ──────────────────────────────────────────
            var studentMap = studentsDb.ToDictionary(s => s.StudentID);
            var lessonMap = lessonsDb.ToDictionary(l => l.Id);
            var sectionMap = sectionsDb.ToDictionary(s => s.Id);
            var batchMap = batchesDb.ToDictionary(b => b.Id);
            var courseMap = coursesDb.ToDictionary(c => c.Id);
            var instructorMap = instructorsDb.ToDictionary(i => i.Id);

            var studentFirstBatch = studentBatchEnrollments
                .GroupBy(e => e.StudentID)
                .ToDictionary(g => g.Key, g => g.First().BatchId);

            var attendanceByStudent = attendanceRecords
                .GroupBy(a => a.StudentId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // ── 4. قوائم الفلاتر ──────────────────────────────────────────────────
            var curriculumsFilter = await _context.Curriculums
                .AsNoTracking()
                .OrderBy(x => x.Title)
                .Select(x => new AnalyticsFilterOptionVM { Id = x.Id, Name = x.Title })
                .ToListAsync();

            var batchesFilter = batchesDb
                .Where(x => x.IsActive && !x.IsDeleted)
                .OrderBy(x => x.Name)
                .Select(x => new AnalyticsFilterOptionVM { Id = x.Id, Name = x.Name })
                .ToList();

            var activeInstructorIds = BuildActiveInstructorIdSet(
                instructorCurriculumBatchesDb, courseInstructorsDb, examAssignments);

            var activeInstructorsDb = activeInstructorIds.Count > 0
                ? instructorsDb.Where(x => activeInstructorIds.Contains(x.Id)).OrderBy(x => x.FullName).ToList()
                : instructorsDb.OrderBy(x => x.FullName).ToList();

            var instructorsFilter = activeInstructorsDb
                .Select(x => new AnalyticsFilterOptionVM { Id = x.Id, Name = x.FullName })
                .ToList();

            // ── 5. تحليل الطلاب ───────────────────────────────────────────────────
            var students = BuildStudentAnalytics(
                examAttempts, studentMap, attendanceByStudent, studentFirstBatch);

            // ── 6. تحليل الدروس الضعيفة ───────────────────────────────────────────
            var lessons = BuildLessonAnalytics(
                examAttempts, lessonMap, batchesDb, studentBatchEnrollments,
                instructorCurriculumBatchesDb, courseInstructorsDb, instructorMap, batchMap);

            // ── 7. تحليل الدفعات (مع pagination) ──────────────────────────────────
            var batchAnalytics = BuildBatchAnalytics(
                examAttempts, batchesDb, studentBatchEnrollments, courseMap);

            var totalPages = batchAnalytics.Count == 0
                ? 1
                : (int)Math.Ceiling(batchAnalytics.Count / (double)PageSize);

            if (page > totalPages) page = totalPages;

            var pagedBatches = batchAnalytics
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            var decisionStatusMap = await _batchDecisionStatusService
                .GetStatusForBatchesAsync(pagedBatches.Select(b => b.BatchId));

            // ── 8. تحليل المدربين ─────────────────────────────────────────────────
            var instructorAnalytics = BuildInstructorAnalytics(
                examAttempts, activeInstructorsDb, instructorCurriculumBatchesDb,
                courseInstructorsDb, examAssignments, studentBatchEnrollments, batchesDb);

            // ── 9. KPIs ───────────────────────────────────────────────────────────
            var avgExam = students.Any() ? Math.Round(students.Average(x => x.ExamScore), 1) : 0;
            var avgHomework = students.Any() ? Math.Round(students.Average(x => x.HomeworkScore), 1) : 0;
            var avgAttendance = students.Any() ? Math.Round(students.Average(x => x.Attendance), 1) : 0;
            var highPriorityCount = students.Count(x => x.Priority == "High");
            var avgWeakness = lessons.Any() ? Math.Round(lessons.Average(x => x.WeakPercentage), 1) : 0;

            var vm = new AdvancedAnalyticsDashboardVM
            {
                Students = students,
                Lessons = lessons,
                Batches = pagedBatches,
                Instructors = instructorAnalytics,

                WeakStudents = students.Count,
                WeakStudentsCount = students.Count(x => x.ExamScore < 60),

                AvgExamScore = avgExam,
                AvgHomeworkScore = avgHomework,
                AvgAttendance = avgAttendance,
                CriticalCases = highPriorityCount,

                TotalBatches = batchesDb.Count(x => x.IsActive && !x.IsDeleted),
                TotalInstructors = activeInstructorsDb.Count,
                HighRiskCount = lessons.Count(x => x.WeakPercentage >= 70),
                AvgWeakness = avgWeakness,

                RiskHigh = batchAnalytics.Count(x => x.AvgWeakness >= 70),
                RiskMedium = batchAnalytics.Count(x => x.AvgWeakness >= 50 && x.AvgWeakness < 70),
                RiskLow = batchAnalytics.Count(x => x.AvgWeakness < 50),

                ExcellentStudents = students.Count(x => x.ExamScore >= 80),
                MediumStudents = students.Count(x => x.ExamScore >= 60 && x.ExamScore < 80),

                InstructorNames = instructorAnalytics.Select(x => x.InstructorName).ToList(),
                InstructorScores = instructorAnalytics.Select(x => x.AvgScore).ToList(),

                CurrentPage = page,
                TotalPages = totalPages,

                SelectedCurriculumId = curriculumId,
                SelectedBatchId = batchId,
                SelectedInstructorId = instructorId,

                CurriculumsFilter = curriculumsFilter,
                BatchesFilter = batchesFilter,
                InstructorsFilter = instructorsFilter,

                DecisionStatusMap = decisionStatusMap
            };

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                .AddExpirationToken(new CancellationChangeToken(GetCacheToken()));

            _cache.Set(cacheKey, vm, cacheOptions);

            return vm;
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private IQueryable<int> BuildInstructorBatchIdsQuery(int instructorId)
        {
            var fromICB = _context.Set<InstructorCurriculumBatch>()
                .Where(x => x.InstructorId == instructorId)
                .Select(x => x.BatchId);

            var courseIds = _context.Set<CourseInstructor>()
                .Where(x => x.InstructorID == instructorId)
                .Select(x => x.CourseID);

            var fromCourses = _context.Set<Batch>()
                .Where(b => courseIds.Contains(b.CourseId))
                .Select(b => b.Id);

            var fromAssignments = _context.Set<ExamAssignmentToBatch>()
                .Where(x => x.CreatedByInstructorId == instructorId)
                .Select(x => x.BatchId);

            return fromICB.Union(fromCourses).Union(fromAssignments);
        }

        private static HashSet<int> BuildActiveInstructorIdSet(
            List<InstructorCurriculumBatch> icb,
            List<CourseInstructor> ci,
            List<ExamAssignmentToBatch> assignments)
        {
            var ids = new HashSet<int>();

            foreach (var x in icb) if (x.InstructorId > 0) ids.Add(x.InstructorId);
            foreach (var x in ci) if (x.InstructorID > 0) ids.Add(x.InstructorID);
            foreach (var x in assignments)
                if (x.CreatedByInstructorId.HasValue && x.CreatedByInstructorId.Value > 0)
                    ids.Add(x.CreatedByInstructorId.Value);

            return ids;
        }

        private static List<StudentAnalyticsVM> BuildStudentAnalytics(
            List<QuestionAttemptNew> attempts,
            Dictionary<int, Student> studentMap,
            Dictionary<int, List<AttendanceRecord>> attendanceByStudent,
            Dictionary<int, int> studentFirstBatch)
        {
            return attempts
                .GroupBy(x => x.StudentId)
                .Select(g =>
                {
                    studentMap.TryGetValue(g.Key, out var student);

                    var examList = g.Where(x =>
                        x.ExamAssignmentId.HasValue ||
                        x.ExamId.HasValue ||
                        x.PerformanceIndicatorExamId.HasValue).ToList();

                    var homeworkList = g.Where(x => x.HomeworkSetId.HasValue).ToList();

                    var examScore = examList.Count == 0
                        ? 0
                        : (examList.Count(x => x.IsCorrect) * 100.0) / examList.Count;

                    var homeworkScore = homeworkList.Count == 0
                        ? 0
                        : (homeworkList.Count(x => x.IsCorrect) * 100.0) / homeworkList.Count;

                    var attendance = attendanceByStudent.GetValueOrDefault(g.Key) ?? new();
                    var attendanceRate = attendance.Count == 0
                        ? 0
                        : (attendance.Count(a => a.IsPresent) * 100.0) / attendance.Count;

                    string weakReason, action, priority;

                    if (examScore < 50 && homeworkScore < 50)
                    {
                        weakReason = "ضعف عام في الفهم";
                        action = "إعادة شرح + واجبات إضافية + اختبار قصير";
                        priority = "High";
                    }
                    else if (examScore < 50)
                    {
                        weakReason = "ضعف في الاختبارات";
                        action = "تدريب على نماذج امتحانات";
                        priority = "High";
                    }
                    else if (homeworkScore < 50)
                    {
                        weakReason = "عدم التزام بالواجبات";
                        action = "متابعة يومية + واجب إجباري";
                        priority = "Medium";
                    }
                    else if (attendanceRate < 50)
                    {
                        weakReason = "غياب متكرر";
                        action = "تنبيه ولي الأمر";
                        priority = "High";
                    }
                    else
                    {
                        weakReason = "أداء متوسط";
                        action = "متابعة فقط";
                        priority = "Low";
                    }

                    studentFirstBatch.TryGetValue(g.Key, out var studentBatchId);

                    return new StudentAnalyticsVM
                    {
                        StudentId = g.Key,
                        StudentName = student?.FullName ?? "غير معروف",
                        ExamScore = Math.Round(examScore, 1),
                        HomeworkScore = Math.Round(homeworkScore, 1),
                        Attendance = Math.Round(attendanceRate, 1),
                        WeakReason = weakReason,
                        ActionRequired = action,
                        Priority = priority,
                        BatchId = studentBatchId > 0 ? studentBatchId : null
                    };
                })
                .Where(x => x.ExamScore < 60 || x.HomeworkScore < 60 || x.Attendance < 60)
                .OrderBy(x => x.ExamScore)
                .ThenBy(x => x.HomeworkScore)
                .Take(50)
                .ToList();
        }

        private static List<LessonAnalyticsVM> BuildLessonAnalytics(
            List<QuestionAttemptNew> attempts,
            Dictionary<int, Lesson> lessonMap,
            List<Batch> batchesDb,
            List<StudentBatchEnrollment> enrollments,
            List<InstructorCurriculumBatch> icb,
            List<CourseInstructor> courseInstructors,
            Dictionary<int, Instructor> instructorMap,
            Dictionary<int, Batch> batchMap)
        {
            var studentFirstEnrollment = enrollments
                .GroupBy(e => e.StudentID)
                .ToDictionary(g => g.Key, g => g.First());

            var icbByBatch = icb.GroupBy(x => x.BatchId)
                .ToDictionary(g => g.Key, g => g.First());

            var cisByCourse = courseInstructors.GroupBy(x => x.CourseID)
                .ToDictionary(g => g.Key, g => g.First());

            return attempts
                .Where(x => x.LessonId.HasValue)
                .GroupBy(x => x.LessonId!.Value)
                .Select(g =>
                {
                    lessonMap.TryGetValue(g.Key, out var lesson);

                    var total = g.Count();
                    var wrong = g.Count(x => !x.IsCorrect);
                    var weakPct = total == 0 ? 0 : (wrong * 100.0) / total;
                    var affected = g.Where(x => !x.IsCorrect).Select(x => x.StudentId).Distinct().Count();

                    string batchName = "-", instructorName = "-";
                    int? lessonBatchId = null;

                    var firstStudentId = g.Select(x => x.StudentId).FirstOrDefault();
                    if (firstStudentId > 0 && studentFirstEnrollment.TryGetValue(firstStudentId, out var enroll))
                    {
                        lessonBatchId = enroll.BatchId;
                        if (batchMap.TryGetValue(enroll.BatchId, out var batch))
                        {
                            batchName = batch.Name;

                            if (icbByBatch.TryGetValue(batch.Id, out var instructorBatch)
                                && instructorMap.TryGetValue(instructorBatch.InstructorId, out var instr))
                            {
                                instructorName = instr.FullName;
                            }
                            else if (cisByCourse.TryGetValue(batch.CourseId, out var ci)
                                && instructorMap.TryGetValue(ci.InstructorID, out var ciInstr))
                            {
                                instructorName = ciInstr.FullName;
                            }
                        }
                    }

                    return new LessonAnalyticsVM
                    {
                        LessonId = g.Key,
                        LessonName = lesson?.Title ?? "درس غير معروف",
                        BatchName = string.IsNullOrWhiteSpace(batchName) ? "-" : batchName,
                        InstructorName = string.IsNullOrWhiteSpace(instructorName) ? "-" : instructorName,
                        AffectedStudents = affected,
                        TotalAttempts = total,
                        StudentsCount = g.Select(x => x.StudentId).Distinct().Count(),
                        WeakPercentage = Math.Round(weakPct, 1),
                        BatchId = lessonBatchId > 0 ? lessonBatchId : null
                    };
                })
                .Where(x => x.WeakPercentage >= 50)
                .OrderByDescending(x => x.WeakPercentage)
                .ThenByDescending(x => x.AffectedStudents)
                .Take(20)
                .ToList();
        }

        private static List<BatchAnalyticsVM> BuildBatchAnalytics(
            List<QuestionAttemptNew> attempts,
            List<Batch> batchesDb,
            List<StudentBatchEnrollment> enrollments,
            Dictionary<int, Course> courseMap)
        {
            var studentToBatch = enrollments
                .GroupBy(e => e.StudentID)
                .ToDictionary(g => g.Key, g => g.First().BatchId);

            var attemptsByBatchStudent = enrollments
                .GroupBy(e => e.BatchId)
                .ToDictionary(
                    g => g.Key,
                    g => new HashSet<int>(g.Select(e => e.StudentID)));

            return batchesDb
                .Where(b => b.IsActive && !b.IsDeleted)
                .Select(batch =>
                {
                    var batchStudentIds = attemptsByBatchStudent.GetValueOrDefault(batch.Id)
                        ?? new HashSet<int>();

                    var batchAttempts = attempts
                        .Where(x => batchStudentIds.Contains(x.StudentId))
                        .ToList();

                    var total = batchAttempts.Count;
                    var wrong = batchAttempts.Count(x => !x.IsCorrect);
                    var avgWeakness = total == 0 ? 0 : (wrong * 100.0) / total;

                    var criticalLessons = batchAttempts
                        .Where(x => x.LessonId.HasValue)
                        .GroupBy(x => x.LessonId!.Value)
                        .Count(lg =>
                        {
                            var lt = lg.Count();
                            var lw = lg.Count(x => !x.IsCorrect);
                            return lt > 0 && (lw * 100.0) / lt >= 50;
                        });

                    var affected = batchAttempts
                        .Where(x => !x.IsCorrect)
                        .Select(x => x.StudentId)
                        .Distinct()
                        .Count();

                    courseMap.TryGetValue(batch.CourseId, out var course);

                    return new BatchAnalyticsVM
                    {
                        BatchId = batch.Id,
                        BatchName = batch.Name,
                        CourseName = course?.Name ?? "-",
                        StudentsCount = batchStudentIds.Count,
                        WeakLessonsCount = criticalLessons,
                        AffectedStudents = affected,
                        CriticalLessonsCount = criticalLessons,
                        AvgWeakness = Math.Round(avgWeakness, 1),
                        RiskLevel = avgWeakness >= 70 ? "High" : avgWeakness >= 50 ? "Medium" : "Low"
                    };
                })
                .Where(x => x.StudentsCount > 0 || x.AvgWeakness > 0)
                .OrderByDescending(x => x.AvgWeakness)
                .ToList();
        }

        private static List<InstructorAnalyticsVM> BuildInstructorAnalytics(
            List<QuestionAttemptNew> attempts,
            List<Instructor> activeInstructors,
            List<InstructorCurriculumBatch> icb,
            List<CourseInstructor> courseInstructors,
            List<ExamAssignmentToBatch> examAssignments,
            List<StudentBatchEnrollment> enrollments,
            List<Batch> batchesDb)
        {
            var enrollmentsByBatch = enrollments
                .GroupBy(e => e.BatchId)
                .ToDictionary(g => g.Key, g => new HashSet<int>(g.Select(e => e.StudentID)));

            var batchesByCourse = batchesDb
                .GroupBy(b => b.CourseId)
                .ToDictionary(g => g.Key, g => g.Select(b => b.Id).ToList());

            return activeInstructors
                .Select(instructor =>
                {
                    var batchIds = new HashSet<int>();

                    foreach (var x in icb)
                        if (x.InstructorId == instructor.Id && x.BatchId > 0)
                            batchIds.Add(x.BatchId);

                    foreach (var x in courseInstructors)
                        if (x.InstructorID == instructor.Id && x.CourseID > 0
                            && batchesByCourse.TryGetValue(x.CourseID, out var bids))
                            foreach (var bid in bids) batchIds.Add(bid);

                    foreach (var x in examAssignments)
                        if (x.CreatedByInstructorId == instructor.Id && x.BatchId > 0)
                            batchIds.Add(x.BatchId);

                    var studentIds = new HashSet<int>();
                    foreach (var bid in batchIds)
                        if (enrollmentsByBatch.TryGetValue(bid, out var sids))
                            foreach (var sid in sids) studentIds.Add(sid);

                    var instructorAttempts = attempts
                        .Where(x => studentIds.Contains(x.StudentId))
                        .ToList();

                    var total = instructorAttempts.Count;
                    var correct = instructorAttempts.Count(x => x.IsCorrect);
                    var avgScore = total == 0 ? 0 : (correct * 100.0) / total;

                    var weakLessons = instructorAttempts
                        .Where(x => x.LessonId.HasValue)
                        .GroupBy(x => x.LessonId!.Value)
                        .Count(lg =>
                        {
                            var lt = lg.Count();
                            var lw = lg.Count(x => !x.IsCorrect);
                            return lt > 0 && (lw * 100.0) / lt >= 50;
                        });

                    return new InstructorAnalyticsVM
                    {
                        InstructorId = instructor.Id,
                        InstructorName = instructor.FullName,
                        AvgScore = Math.Round(avgScore, 1),
                        WeakLessonsCount = weakLessons,
                        BatchesCount = batchIds.Count,
                        RiskLevel = avgScore < 50 ? "High" : avgScore < 70 ? "Medium" : "Low"
                    };
                })
                .Where(x => x.BatchesCount > 0)
                .OrderBy(x => x.AvgScore)
                .ThenByDescending(x => x.WeakLessonsCount)
                .ToList();
        }

        // ── cache token ──────────────────────────────────────────────────────────

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
            {
                cts.Cancel();
                cts.Dispose();
            }

            _cache.Remove(CacheTokenKey);
        }
    }
}
