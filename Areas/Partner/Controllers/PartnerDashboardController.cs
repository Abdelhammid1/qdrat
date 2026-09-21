using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Interfaces;
using QdratNew.Services.Partner.Interfaces;
using QdratNew.ViewModels.Partner;
using QdratNew.ViewModels.Partner.Dashboard;
using QdratNew.ViewModels.Partner.Exam;
using QdratNew.ViewModels.Partner.Homework;
using QdratNew.ViewModels.Partner.Risk;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    public class DashboardController : PartnerBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IPartnerHomeworkInsightsService _homeworkService;
        private readonly IPartnerExamInsightsService _examService;
        private readonly IPartnerRiskService _riskService;
        public DashboardController(
            ApplicationDbContext context,
            IPartnerSubscriptionService subscriptionService, IPartnerHomeworkInsightsService homeworkService, IPartnerExamInsightsService examService, IPartnerRiskService riskService)
            : base(subscriptionService, context)
        {
            _context = context;
            _homeworkService = homeworkService;
            _examService = examService;
            _riskService = riskService;
        }


        public async Task<IActionResult> Index()
        {
            // ===============================
            // 1️⃣ تحميل البيانات الأساسية
            // ===============================
            var students = await _context.Students
                .Where(s => s.Branch.PartnerId == ActivePartnerId)
                .ToListAsync();

            var batches = await _context.Batches
                .Where(b => b.Branch.PartnerId == ActivePartnerId)
                .ToListAsync();
            var enrollments = await _context.StudentBatchEnrollments
                .Include(e => e.Batch)
                    .ThenInclude(b => b.Branch)
                .Where(e => e.Batch.Branch.PartnerId == ActivePartnerId)
                .ToListAsync();

            // ===============================
            // 2️⃣ تجهيز Sets (أداء أعلى)
            // ===============================
            var studentIdsWithBatch = enrollments
                .Select(e => e.StudentID)
                .Distinct()
                .ToHashSet();

            // ===============================
            // 3️⃣ توزيع الطلاب حسب الدورات
            // ===============================
            var courseDistribution = enrollments
                .GroupBy(e => e.Batch.Course.Name)
                .Select(g => new
                {
                    CourseName = g.Key ?? "غير محدد",
                    Count = g.Select(x => x.StudentID).Distinct().Count()
                })
                .ToList();

            // ===============================
            // 4️⃣ حسابات موحدة (بدون تكرار)
            // ===============================
            var totalStudents = students.Count;
            var activeStudents = students.Count(s => s.IsActiveForLearning);
            var inactiveStudents = totalStudents - activeStudents;

            var studentsWithoutBatch = students
                .Count(s => !studentIdsWithBatch.Contains(s.StudentID));

            // ===============================
            // 5️⃣ Latest + Risk (محمي من Null)
            // ===============================
            var latestStudents = students
                .OrderByDescending(s => s.StudentID)
                .Take(5)
                .Select(s => s.FullName ?? "")
                .ToList();

            var riskStudents = await _riskService.CalculateRiskAsync(ActivePartnerId);

            // ===============================
            // 6️⃣ بناء الموديل
            // ===============================
            var model = new PartnerDashboardAdvancedVM
            {
                TotalStudents = totalStudents,
                ActiveStudents = activeStudents,
                ArchivedStudents = inactiveStudents,

                TotalBatches = batches.Count,

                StudentsWithNoBatch = studentsWithoutBatch,
                StudentsWithoutActivity = studentsWithoutBatch,

                HighPerformingStudents = activeStudents,
                AtRiskStudents = inactiveStudents,
                InactiveStudents = inactiveStudents,

                LatestStudents = latestStudents ?? new List<string>(),
                RiskStudents = riskStudents ?? new List<StudentRiskVM>(),

                CourseLabels = courseDistribution.Select(x => x.CourseName).ToList(),
                CourseCounts = courseDistribution.Select(x => x.Count).ToList()
            };

            // ===============================
            // 7️⃣ تحميل تقرير الواجبات (محمي)
            // ===============================
            var homeworkVM = await _homeworkService.GetDashboardAsync(ActivePartnerId);
            ViewBag.Homework = homeworkVM ?? new HomeworkDashboardVM();

            var risks = await _riskService.CalculateRiskAsync(ActivePartnerId);

            ViewBag.RiskStudents = risks.Take(10).ToList();

            ViewBag.RiskStats = new int[]
            {
    risks.Count(x => x.RiskLevel == "Safe"),
    risks.Count(x => x.RiskLevel == "Warning"),
    risks.Count(x => x.RiskLevel == "Risk")
            };

            var examVM = await _examService.GetDashboardAsync(ActivePartnerId);
            ViewBag.Exam = examVM ?? new ExamDashboardVM();
            // ===============================
            // 8️⃣ Return
            // ===============================
            return View(model);
        }

    }
}