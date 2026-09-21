using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services;
using QdratNew.Services.AI;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Students;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    public class StudentProgressController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RemedialPlanService _remedialPlanService;
        private readonly NotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentProgressController(
            ApplicationDbContext context,
            NotificationService notificationService,
            RemedialPlanService remedialPlanService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _notificationService = notificationService;
            _remedialPlanService = remedialPlanService;
            _userManager = userManager;
        }

        // ✅ عرض الأداء الأكاديمي وسجل الحضور
        public async Task<IActionResult> Details()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var student = await _context.Students
                .Include(s => s.StudentPerformances)
                    .ThenInclude(sp => sp.Curriculum)
                .Include(s => s.StudentPerformances)
                    .ThenInclude(sp => sp.Section)
                .FirstOrDefaultAsync(s => s.UserId == currentUser.Id);

            if (student == null) return NotFound();

            var lectureAttendances = await _context.AttendanceRecords
                .Where(a => a.StudentId == student.StudentID)
                .Include(a => a.Lecture)
                    .ThenInclude(l => l.Course)
                .Include(a => a.Lecture.Section)
                .OrderByDescending(a => a.Lecture.Date)
                .ToListAsync();

            var attendanceList = lectureAttendances.Select(a => new LectureAttendanceStatus
            {
                LectureTitle = a.Lecture.Title,
                Date = a.Lecture.Date,
                Section = a.Lecture.Section?.Title ?? "—",
                Course = a.Lecture.Course?.Name ?? "—",
                IsPresent = a.IsPresent
            }).ToList();

            var viewModel = new StudentProgressViewModel
            {
                StudentID = student.StudentID,
                FullName = student.FullName,
                PerformanceRecords = student.StudentPerformances.Select(sp => new PerformanceItem
                {
                    CurriculumTitle = sp.Curriculum?.Title ?? "—",
                    SectionTitle = sp.Section?.Title ?? "—",
                    Score = sp.Score,
                    Date = sp.ExamDate
                }).ToList(),
                AttendanceRecords = attendanceList
            };

            return View(viewModel);
        }

        // ✅ تحليل الذكاء الاصطناعي
        public async Task<IActionResult> AIAnalysis()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var student = await _context.Students
                .Include(s => s.StudentPerformances)
                    .ThenInclude(sp => sp.Section)
                .Include(s => s.StudentPerformances)
                    .ThenInclude(sp => sp.Curriculum)
                .FirstOrDefaultAsync(s => s.UserId == currentUser.Id);

            if (student == null) return NotFound("الطالب غير موجود.");

            // 🔍 الجلسة القادمة
            var nextSession = _context.RemedialSessions
                .Where(s => s.StudentID == student.StudentID && s.ScheduledDate > DateTime.Now && !s.IsCompleted)
                .OrderBy(s => s.ScheduledDate)
                .FirstOrDefault();

            // 🧠 تحليل الأداء
            var report = AIStudentPerformanceAnalyzer.Analyze(student.StudentPerformances.ToList());

            // 🟡 التحقق وإنشاء خطة علاجية
            var hasActivePlan = _context.RemedialPlans
                .Any(r => r.StudentID == student.StudentID && r.IsCompleted != true);
            if (!hasActivePlan && report.AverageScore < 60)
            {
                var created = _remedialPlanService.CreateRemedialPlan(student.StudentID);
                if (created)
                {
                    _notificationService.SendNotification(student.StudentID, "📢 تم إنشاء خطة علاجية لك بناءً على نتائجك الأخيرة.", "خطة علاجية");

                    var parent = _context.Parents
                        .Include(p => p.Students)
                        .FirstOrDefault(p => p.Students.Any(s => s.StudentID == student.StudentID));

                    if (parent != null)
                    {
                        _notificationService.SendNotification(
                            parent.ParentID,
                            $"📢 تم إنشاء خطة علاجية لابنك {student.FullName} بناءً على نتائج الأداء الأخيرة.",
                            "متابعة أولياء الأمور"
                        );
                    }
                }
            }

            // ✅ التوصيات العلاجية
            var remedialRecommendations = AIStudentRemedialPlanGenerator.Generate(student.StudentPerformances.ToList());

            // 📊 الحضور
            var attendances = await _context.AttendanceRecords
                .Where(a => a.StudentId == student.StudentID)
                .Include(a => a.Lecture)
                .OrderByDescending(a => a.Lecture.Date)
                .ToListAsync();

            int totalLectures = attendances.Count;
            int attendedLectures = attendances.Count(a => a.IsPresent);
            var lastLecture = attendances.FirstOrDefault();

            // ✅ ViewModel النهائي
            var viewModel = new StudentAIReportViewModel
            {
                StudentID = student.StudentID,
                StudentName = student.FullName,

                AverageScore = report.AverageScore,
                SuccessRate = report.SuccessRate,
                AIRecommendations = report.Recommendations,
                NextSession = nextSession,

                SectionSummaries = report.SectionSummaries?.Select(s => new SectionPerformanceSummary
                {
                    CurriculumId = s.CurriculumId,
                    CurriculumTitle = s.CurriculumTitle,
                    SectionTitle = s.SectionTitle,
                    AverageScore = Math.Round(s.AverageScore, 2),
                    SuccessRate = Math.Round(s.SuccessRate, 1),
                    DifficultyLevel = s.DifficultyLevel
                }).ToList() ?? new List<SectionPerformanceSummary>(),

                RemedialRecommendations = remedialRecommendations,

                RemedialSessions = _context.RemedialSessions
                    .Where(rs => rs.StudentID == student.StudentID)
                    .OrderBy(rs => rs.ScheduledDate)
                    .Select(rs => new RemedialSessionViewModel
                    {
                        SessionId = rs.Id,
                        SectionId = rs.SectionId ?? 0,
                        SectionTitle = rs.SectionTitle,
                        ScheduledDate = rs.ScheduledDate,
                        StartTime = rs.StartTime ?? TimeSpan.Zero,
                        EndTime = rs.EndTime ?? TimeSpan.Zero,
                        RoomName = rs.RoomName,
                        Resources = rs.Resources,
                        IsConfirmed = rs.IsConfirmed
                    }).ToList(),

                // 🔗 بيانات الحضور
                TotalLectures = totalLectures,
                AttendedLectures = attendedLectures,
                LastLectureTitle = lastLecture?.Lecture?.Title,
                LastLectureDate = lastLecture?.Lecture?.Date
            };

            return View(viewModel);
        }




    }
}
