using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Areas.Admin.ViewModels;
using QdratNew.Data;
using QdratNew.ViewModels.Batch;
using QdratNew.ViewModels.Question;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminArea")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        public HomeController(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<IActionResult> Index()
        {
            const string cacheKey = "ADMIN_DASHBOARD_INDEX";

            if (_cache.TryGetValue(cacheKey, out AdminDashboardViewModel cachedModel))
                return View(cachedModel);

            var model = new AdminDashboardViewModel();

            // =====================================================
            // 1) إحصائيات سريعة
            // =====================================================
            model.TotalUsers = await _context.Users.CountAsync();
            model.TotalStudents = await _context.Students.CountAsync();
            model.ActiveBatches = await _context.Batches.CountAsync(b => b.IsActive);
            model.TotalExams = await _context.Exams.CountAsync();
            model.TotalQuestions = await _context.Questions.CountAsync();

            // =====================================================
            // 2) الواجبات (تحميل واحد)
            // =====================================================
            var sentHomeworks = await _context.Homeworks
                .Where(h => h.IsSent)
                .Select(h => new
                {
                    h.StudentId,
                    h.IsCompleted,
                    h.AssignedAt
                })
                .AsNoTracking()
                .ToListAsync();

            model.SentHomeworks = sentHomeworks.Count;
            model.UnsolvedHomeworks = sentHomeworks.Count(x => !x.IsCompleted);

            var groupedByStudent = sentHomeworks
                .GroupBy(x => x.StudentId)
                .ToList();

            model.StudentsDidNotSolveLastHomework =
                groupedByStudent.Count(g =>
                {
                    var last = g.OrderByDescending(x => x.AssignedAt).FirstOrDefault();
                    return last != null && !last.IsCompleted;
                });

            model.StudentsLateTwoHomeworks =
                groupedByStudent.Count(g =>
                    g.OrderByDescending(x => x.AssignedAt)
                     .Take(2)
                     .Count(x => !x.IsCompleted) >= 2);

            // =====================================================
            // 3) الخطط العلاجية
            // =====================================================
            var remedialPlans = await _context.RemedialPlans
                .Select(p => new
                {
                    Sessions = p.Sessions.Select(s => s.IsCompleted).ToList()
                })
                .AsNoTracking()
                .ToListAsync();

            model.TotalRemedialPlans = remedialPlans.Count;
            model.StudentsWithPlansStarted = remedialPlans.Count(p => p.Sessions.Any(x => x));
            model.StudentsWithPlansNotResponded = remedialPlans.Count(p => p.Sessions.Count == 0);

            // =====================================================
            // 4) بنك الأسئلة (تحميل جداول منفصلة – تجميع بالذاكرة)
            // =====================================================
            var curriculums = await _context.Curriculums
                .AsNoTracking()
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            var sections = await _context.Sections
                .AsNoTracking()
                .Select(s => new { s.Id, s.Title, s.CurriculumId })
                .ToListAsync();

            var lessons = await _context.Lessons
                .Where(l => l.IsActive)
                .AsNoTracking()
                .Select(l => new { l.Id, l.SectionId })
                .ToListAsync();

            var questions = await _context.Questions
                .AsNoTracking()
                .Select(q => new { q.LessonId })
                .ToListAsync();

            model.QuestionBankStatsByCurriculum =
                curriculums.Select(c => new QuestionBankByCurriculumViewModel
                {
                    CurriculumTitle = c.Title,
                    Sections = sections
                        .Where(s => s.CurriculumId == c.Id)
                        .Select(s =>
                        {
                            var lessonIds = lessons
                                .Where(l => l.SectionId == s.Id)
                                .Select(l => l.Id)
                                .ToList();

                            var questionCount = questions
                                .Count(q => lessonIds.Any(id => id == q.LessonId));

                            return new QuestionBankStatsViewModel
                            {
                                SectionId = s.Id,
                                SectionTitle = s.Title,
                                LessonsCount = lessonIds.Count,
                                QuestionsCount = questionCount
                            };
                        })
                        .ToList()
                })
                .ToList();

            // =====================================================
            // 5) آخر أنشطة الإدارة
            // =====================================================
            model.RecentAdminActivities = await _context.AdminActivityLogs
                .AsNoTracking()
                .OrderByDescending(a => a.Timestamp)
                .Take(10)
                .ToListAsync();

            // =====================================================
            // 6) إشعارات الأدمن
            // =====================================================
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            model.AdminNotifications = await _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.SentAt)
                .Take(10)
                .ToListAsync();

            // =====================================================
            // 7) تنبيهات تسجيل المؤشرات (بدون Contains)
            // =====================================================
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var todayLecturesBatchIds = await _context.Lecture
                .Where(l => l.Date >= today && l.Date < tomorrow)
                .Select(l => l.BatchId)
                .Distinct()
                .ToListAsync();

            var completedBatchIds = await _context.BatchLessonCompletions
                .Where(b => b.CompletionDate >= today && b.CompletionDate < tomorrow)
                .Select(b => b.BatchId)
                .Distinct()
                .ToListAsync();

            var missingBatchIds = todayLecturesBatchIds
                .Where(id => !completedBatchIds.Any(x => x == id))
                .ToList();

            var allBatches = await _context.Batches
                .AsNoTracking()
                .Select(b => new { b.Id, b.Name })
                .ToListAsync();

            model.BatchesMissingLessonsNotifications =
                allBatches
                    .Where(b => missingBatchIds.Any(id => id == b.Id))
                    .Select(b => $"⚠️ الدفعة ({b.Name}) لم يتم تسجيل المؤشرات اليوم.")
                    .ToList();

            // =====================================================
            // 8) Cache
            // =====================================================
            _cache.Set(
                cacheKey,
                model,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                });

            return View(model);
        }


    



        [HttpGet]
        public async Task<IActionResult> GetPerformanceStats()
        {
            var totalStudents = await _context.Students.CountAsync();

            var lastHomeworkSolved = totalStudents == 0 ? 0 :
                Math.Round(100.0 * await _context.Homeworks
                    .Where(h => h.IsSent)
                    .GroupBy(h => h.StudentId)
                    .Where(g => g.OrderByDescending(h => h.AssignedAt).First().IsCompleted == true)
                    .CountAsync() / (double)totalStudents, 2);

            var lateTwoHomeworks = totalStudents == 0 ? 0 :
                Math.Round(100.0 * await _context.Homeworks
                    .Where(h => h.IsSent)
                    .GroupBy(h => h.StudentId)
                    .Where(g => g.OrderByDescending(h => h.AssignedAt).Take(2).Count(h => !h.IsCompleted) >= 2)
                    .CountAsync() / (double)totalStudents, 2);

            var startedRemedial = totalStudents == 0 ? 0 :
                Math.Round(100.0 * await _context.RemedialPlans
                    .Where(p => p.Sessions.Any(s => s.IsCompleted))
                    .Select(p => p.StudentID)
                    .Distinct()
                    .CountAsync() / (double)totalStudents, 2);

            var noHomeworksThisWeek = totalStudents == 0 ? 0 :
                Math.Round(100.0 * await _context.Homeworks
                    .Where(h => h.IsSent && h.AssignedAt >= DateTime.Now.AddDays(-7))
                    .GroupBy(h => h.StudentId)
                    .Where(g => g.All(h => !h.IsCompleted))
                    .CountAsync() / (double)totalStudents, 2);

            return Json(new
            {
                LastHomeworkSolvedPercentage = lastHomeworkSolved,
                LateTwoHomeworksPercentage = lateTwoHomeworks,
                StartedRemedialPlanPercentage = startedRemedial,
                DidNotSolveAnyThisWeekPercentage = noHomeworksThisWeek
            });
        }




        [HttpPost]
        [Area("Admin")]
        public IActionResult TrainBranchModel()
        {
            try
            {
                var trainingData = (from b in _context.Branches
                                    let students = _context.Students.Count(s => s.BranchId == b.Id)
                                    let courses = _context.Courses.Count(c => c.BranchId == b.Id)
                                    let projects = _context.Projects.Count(p => p.BranchId == b.Id)
                                    let attendance = _context.AttendanceRecords
                                        .Where(a => a.Student.BranchId == b.Id)
                                        .Select(a => a.IsPresent ? 1 : 0)
                                        .ToList()
                                    let avgAttendance = attendance.Any() ? attendance.Average() * 100 : 0
                                    let scores = _context.StudentPerformances
                                        .Where(sp => sp.Student.BranchId == b.Id)
                                        .Select(sp => sp.Score)
                                        .ToList() // ✅ هنا التعديل المهم
                                    let avgPerformance = scores.Any() ? scores.Average() : 0
                                    select new BranchPerformanceInput
                                    {
                                        TotalStudents = students,
                                        TotalCourses = courses,
                                        TotalProjects = projects,
                                        AverageAttendance = (float)avgAttendance,
                                        AverageEngagement = (float)(scores.Any() ? scores.Count(s => s >= 60) * 100.0 / scores.Count() : 0),
                                        AveragePerformance = (float)avgPerformance
                                    }).ToList();

                if (trainingData.Count < 1)
                    return Json(new { success = false, message = "⚠️ لا توجد بيانات كافية لتوليد النموذج." });

                BranchPerformanceTrainer.TrainModel(trainingData);
                return Json(new { success = true, message = "✅ تم تدريب النموذج بنجاح." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ خطأ: " + ex.Message });
            }
        }




    }
}
