using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Analytics;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Homework.Interfaces;
using QdratNew.Services.Interfaces;
using QdratNew.Services.StudentProgress;
using QdratNew.ViewModels.Homework;
using System.Text.Json;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class StudentHomeworkDashboardNewController : StudentBaseController
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IStudentAnalyticsService _analyticsService;
        private readonly IStudentHomeworkAnalyticsService _homeworkAnalyticsService;
        private readonly IHomeworkRecommendationService _homeworkRecommendationService;
        private readonly IStudentRankingService _studentRankingService;
        private readonly ITimeZoneService _time;
        private readonly IStudentHomeworkStatusService _homeworkStatusService;
        private readonly IMemoryCache _cache;

        // مدة صلاحية كاش متوسط الدفعة لكل واجب. القيمة دي إحصائية عرض فقط (Chart)
        // مش أساس تصحيح أو درجة، فهامش تأخر بضع دقائق مقبول مقابل توفير Full Scan
        // على جدول QuestionAttemptNew لكل طالب بيفتح لوحة التحكم.
        private static readonly TimeSpan BatchStatsCacheDuration = TimeSpan.FromMinutes(5);

        public StudentHomeworkDashboardNewController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IStudentAnalyticsService analyticsService,
            IStudentHomeworkAnalyticsService homeworkAnalyticsService,
            IHomeworkRecommendationService homeworkRecommendationService,
            IStudentRankingService studentRankingService, ITimeZoneService time, IStudentHomeworkStatusService homeworkStatusService,
            IMemoryCache cache
        ) : base(contextFactory, userManager)
        {
            _contextFactory = contextFactory;
            _analyticsService = analyticsService;
            _homeworkAnalyticsService = homeworkAnalyticsService;
            _homeworkRecommendationService = homeworkRecommendationService;
            _studentRankingService = studentRankingService;
            _time = time;
            _homeworkStatusService = homeworkStatusService;
            _cache = cache;
        }

        // =====================================================
        // Dashboard
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            using var db = _contextFactory.CreateDbContext();

            var studentId = StudentId;
            var batchId = ActiveBatchId;
            var courseId = ActiveCourseId;

            // =====================================================
            // 1️⃣ Counters من المصدر الموحد
            // =====================================================

            var summary = await _homeworkStatusService
                .GetHomeworkSummaryAsync(studentId, courseId, batchId);

            // =====================================================
            // 2️⃣ آخر واجب محلول
            // =====================================================

            var lastSolvedSetId = await db.HomeworkSetStudents
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.IsSubmitted &&
                    x.SubmittedAt != null)
                .OrderByDescending(x => x.SubmittedAt)
                .Select(x => x.HomeworkSetId)
                .FirstOrDefaultAsync();

            // =====================================================
            // 3️⃣ مناهج الدورة الحالية
            // =====================================================

            var curriculums = await (
                from cc in db.CourseCurriculums.AsNoTracking()
                join c in db.Curriculums.AsNoTracking()
                    on cc.CurriculumId equals c.Id
                where cc.CourseId == courseId
                select new CurriculumViewModel
                {
                    Id = c.Id,
                    Title = c.Title
                }
            ).Distinct().ToListAsync();

            var vm = new StudentHomeworkDashboardViewModel
            {
                TotalAssigned = summary.Total,
                Completed = summary.Completed,
                Pending = summary.Required,
                Late = summary.Late,
                Curriculums = curriculums
            };

            // =====================================================
            // 4️⃣ التحليل الذكي لآخر واجب
            // =====================================================

            if (lastSolvedSetId > 0)
            {
                vm.LastHomeworkAnalysis =
                    await _homeworkAnalyticsService
                        .AnalyzeHomeworkAsync(studentId, lastSolvedSetId);

                if (vm.LastHomeworkAnalysis != null)
                {
                    vm.Recommendation =
                        _homeworkRecommendationService
                            .GenerateRecommendation(vm.LastHomeworkAnalysis);
                }
            }

            return View(vm);
        }


        // =====================================================
        // Curriculum Charts
        // =====================================================




        [HttpGet]
        public async Task<IActionResult> GetCurriculumData(int curriculumId)
        {
            using var db = _contextFactory.CreateDbContext();

            int studentId = StudentId;
            int batchId = ActiveBatchId;
            int courseId = ActiveCourseId;

            // ==================================================
            // 1️⃣ محاولات الطالب الخاصة بالدفعة والمنهج
            // ==================================================
            var studentAttemptsQuery =
                from qa in db.QuestionAttemptNew.AsNoTracking()
                join hs in db.HomeworkSets.AsNoTracking()
                    on qa.HomeworkSetId equals hs.Id
                join b in db.Batches.AsNoTracking()
                    on hs.BatchId equals b.Id
                join q in db.Questions.AsNoTracking()
                    on qa.QuestionId equals q.Id
                join l in db.Lessons.AsNoTracking()
                    on q.LessonId equals l.Id
                join s in db.Sections.AsNoTracking()
                    on l.SectionId equals s.Id
                join c in db.Curriculums.AsNoTracking()
                    on s.CurriculumId equals c.Id
                where qa.StudentId == studentId
                      && qa.HomeworkSetId != null
                      && hs.BatchId == batchId
                      && b.CourseId == courseId
                select new
                {
                    HomeworkSetId = qa.HomeworkSetId.Value,
                    qa.QuestionId,
                    qa.IsCorrect,
                    qa.AttemptedAt,
                    SectionId = s.Id,
                    SectionTitle = s.Title,
                    CurriculumId = c.Id
                };

            if (curriculumId > 0)
                studentAttemptsQuery = studentAttemptsQuery
                    .Where(a => a.CurriculumId == curriculumId);

            var studentAttempts = await studentAttemptsQuery.ToListAsync();

            if (!studentAttempts.Any())
                return Json(EmptyCharts());

            // ==================================================
            // 2️⃣ آخر Attempt لكل سؤال
            // ==================================================
            var finalStudentAttempts = studentAttempts
                .GroupBy(a => new { a.HomeworkSetId, a.QuestionId })
                .Select(g => g.OrderByDescending(x => x.AttemptedAt).First())
                .ToList();

            // ==================================================
            // 3️⃣ Radar
            // ==================================================
            var radar = finalStudentAttempts
                .GroupBy(x => new { x.SectionId, x.SectionTitle })
                .OrderBy(x => x.Key.SectionTitle)
                .Select(g =>
                {
                    int total = g.Count();
                    int correct = g.Count(x => x.IsCorrect == true);

                    return new
                    {
                        g.Key.SectionId,
                        g.Key.SectionTitle,
                        StudentScore = total == 0
                            ? 0
                            : Math.Round(correct * 100.0 / total, 1)
                    };
                })
                .ToList();

            // ==================================================
            // 4️⃣ Progress + Comparison (كما كان يعمل سابقًا)
            // ==================================================
            var homeworkGroups = finalStudentAttempts
                .GroupBy(x => x.HomeworkSetId)
                .OrderBy(x => x.Key)
                .ToList();

            var homeworkTitles = new List<string>();
            var correctAnswers = new List<int>();
            var remainingQuestions = new List<int>();
            var studentHomeworkScores = new List<double>();
            var batchHomeworkAverages = new List<double>();

            // متوسط الدفعة لكل واجب: بيانات عرض فقط (Chart) ومشتركة بين كل طلاب الدفعة،
            // فبدل ما كل طالب يفتح لوحة التحكم يعمل تجميع (Group By) على كامل جدول
            // QuestionAttemptNew لكل طلاب الدفعة، بنحسبها مرة واحدة ونخزنها في الكاش
            // لمدة قصيرة (BatchStatsCacheDuration) لأن نفس الحساب هيتكرر بالظبط لأي
            // طالب تاني في نفس الدفعة يفتح نفس الشاشة خلال نفس المدة.
            var batchStatsCacheKey = $"CurriculumDashboard:BatchHomeworkAverages:{batchId}";

            var batchAverageByHomeworkSet = await _cache.GetOrCreateAsync(
                batchStatsCacheKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = BatchStatsCacheDuration;

                    var batchHomeworkStats = await (
                        from qa in db.QuestionAttemptNew.AsNoTracking()
                        join hs in db.HomeworkSets.AsNoTracking()
                            on qa.HomeworkSetId equals hs.Id
                        where qa.HomeworkSetId != null
                              && hs.BatchId == batchId
                        group qa by new
                        {
                            HomeworkSetId = qa.HomeworkSetId.Value,
                            qa.StudentId
                        }
                        into g
                        select new
                        {
                            g.Key.HomeworkSetId,
                            g.Key.StudentId,
                            Total = g.Count(),
                            Correct = g.Count(x => x.IsCorrect)
                        }
                    ).ToListAsync();

                    return batchHomeworkStats
                        .GroupBy(x => x.HomeworkSetId)
                        .ToDictionary(
                            g => g.Key,
                            g => Math.Round(
                                g.Average(x => x.Total == 0
                                    ? 0
                                    : x.Correct * 100.0 / x.Total),
                                1));
                });

            int index = 1;

            foreach (var hw in homeworkGroups)
            {
                int total = hw.Count();
                int correct = hw.Count(x => x.IsCorrect == true);

                homeworkTitles.Add($"واجب {index++}");
                correctAnswers.Add(correct);
                remainingQuestions.Add(Math.Max(0, total - correct));

                double score = total == 0
                    ? 0
                    : Math.Round(correct * 100.0 / total, 1);

                studentHomeworkScores.Add(score);

                batchHomeworkAverages.Add(
                    batchAverageByHomeworkSet.TryGetValue(hw.Key, out double batchAverage)
                        ? batchAverage
                        : 0
                );
            }

            // ==================================================
            // 5️⃣ تجهيز Radar Arrays
            // ==================================================
            var sectionLabels = radar.Select(x => x.SectionTitle).ToList();
            var studentScores = radar.Select(x => x.StudentScore).ToList();
            var batchAverages = radar.Select(x => 0.0).ToList(); // مؤقتًا كما كان سابقًا

            // ==================================================
            // 6️⃣ JSON النهائي (بدون كسر الواجهة)
            // ==================================================
            return Json(new
            {
                sectionLabels,
                studentScores,
                batchAverages,

                homeworkTitles,
                correctAnswers,
                remainingQuestions,

                studentHomeworkScores,
                batchHomeworkAverages
            });
        }




        // =====================================================
        // Attendance Summary (lecture tiles for student)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> GetAttendanceSummary(int curriculumId = 0)
        {
            using var db = _contextFactory.CreateDbContext();
            int studentId = StudentId;
            int batchId   = ActiveBatchId;

            // ① Get section IDs if curriculum filter applied
            List<int>? sectionIds = null;
            if (curriculumId > 0)
            {
                sectionIds = await db.Sections.AsNoTracking()
                    .Where(s => s.CurriculumId == curriculumId)
                    .Select(s => s.Id)
                    .ToListAsync();

                if (!sectionIds.Any())
                    return Json(new { grouped = new object[] { } });
            }

            // ② Get lectures for this batch (optionally filtered by section)
            var lecturesQuery = db.Lecture.AsNoTracking()
                .Where(l => l.BatchId == batchId);

            if (sectionIds != null)
                lecturesQuery = lecturesQuery.Where(l => sectionIds.Contains(l.SectionId));

            var lectures = await lecturesQuery
                .OrderBy(l => l.Date)
                .Select(l => new
                {
                    l.Id,
                    l.Title,
                    Date    = l.Date,
                    SectionId = l.SectionId
                })
                .ToListAsync();

            if (!lectures.Any())
                return Json(new { grouped = new object[] { } });

            // ③ Get section titles
            var sectionIdList = lectures.Select(l => l.SectionId).Distinct().ToList();
            var sectionTitles = await db.Sections.AsNoTracking()
                .Where(s => sectionIdList.Contains(s.Id))
                .Select(s => new { s.Id, s.Title })
                .ToDictionaryAsync(s => s.Id, s => s.Title);

            // ④ Get attendance records for this student
            var lectureIds = lectures.Select(l => l.Id).ToList();
            var records = await db.AttendanceRecords.AsNoTracking()
                .Where(a => a.StudentId == studentId && lectureIds.Contains(a.LectureId))
                .Select(a => new { a.LectureId, a.IsPresent, a.IsLateArrival })
                .ToListAsync();

            var attMap = records.ToDictionary(a => a.LectureId);

            // ⑤ Build result per section
            var grouped = lectures
                .GroupBy(l => l.SectionId)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    sectionTitles.TryGetValue(g.Key, out var secTitle);
                    int idx = 0;
                    return new
                    {
                        section = secTitle ?? "محاضرات عامة",
                        lectures = g.Select(l =>
                        {
                            idx++;
                            attMap.TryGetValue(l.Id, out var rec);
                            string status = rec == null   ? "no-record"
                                          : !rec.IsPresent ? "absent"
                                          : rec.IsLateArrival ? "late"
                                          : "present";
                            return new
                            {
                                index = idx,
                                title = l.Title,
                                date  = l.Date.ToString("yyyy/MM/dd"),
                                status
                            };
                        }).ToList()
                    };
                })
                .ToList();

            return Json(new { grouped });
        }

        private object EmptyCharts()
        {
            return new
            {
                sectionLabels = new List<string>(),
                studentScores = new List<double>(),
                batchAverages = new List<double>(),

                homeworkTitles = new List<string>(),
                correctAnswers = new List<int>(),
                remainingQuestions = new List<int>(),

                studentHomeworkScores = new List<double>(),
                batchHomeworkAverages = new List<double>()
            };
        }

    

        // =====================================================
        // Recommendations
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Recommendations(int curriculumId)
        {
            var analytics =
                await _analyticsService.AnalyzeStudentAsync(StudentId, curriculumId);

            return Json(analytics);
        }

        // =====================================================
        // Ranking
        // =====================================================
        [HttpPost]
        public async Task<IActionResult> RefreshRank(int batchId)
        {
            await _studentRankingService
                .RecordStudentRankAsync(StudentId, batchId);

            using var db = _contextFactory.CreateDbContext();

            var lastRank = await db.StudentRankHistories
                .Where(r => r.StudentId == StudentId && r.BatchId == batchId)
                .OrderByDescending(r => r.RecordedAt)
                .FirstOrDefaultAsync();

            return Json(new
            {
                success = true,
                rank = lastRank?.Rank ?? 0,
                total = lastRank?.TotalStudents ?? 0,
                updated = lastRank?.RecordedAt.ToString("yyyy-MM-dd HH:mm")
            });
        }
    }
}
