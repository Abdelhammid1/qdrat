using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.DTOs;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.AI;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Batch;
// أعلى الملف
using StudentVM = QdratNew.ViewModels.StudentViewModel;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class BatchesDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BatchesDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // 1) تحميل الدُفعات + أسماء الفرع/الكورس بدون Include (Projection)
            var batches = _context.Batches
     .AsNoTracking()
     .Select(b => new BatchProjection
     {
         Id = b.Id,
         Name = b.Name,
         Gender = (GenderType)Enum.Parse(typeof(GenderType), b.Gender.ToString()),
         CourseName = b.Course != null ? b.Course.Name : null,
         BranchName = b.Branch != null ? b.Branch.Name : null,
         BranchState = b.Branch != null ? b.Branch.State : null,
         BranchLocation = b.Branch != null ? b.Branch.Location : null
     })
     .ToList();



            // 2) الطلاب (هنحتاج العدّ الإجمالي + إحصاء النوع)
            var students = _context.Students
                .AsNoTracking()
                .Select(s => new Student
                {
                    StudentID = s.StudentID,
                    Gender = s.Gender
                })
                .ToList();

            // 3) أداء الطلاب
            var performances = _context.StudentPerformances
                .AsNoTracking()
                .Select(p => new StudentPerformance
                {
                    Id = p.Id,
                    StudentID = p.StudentID,
                    Score = p.Score
                })
                .ToList();

            // 4) اشتراكات الدُفعات (جدول الربط الجديد)
            var enrollments = _context.StudentBatchEnrollments
                .AsNoTracking()
                .Select(e => new StudentBatchEnrollment
                {
                    StudentID = e.StudentID,
                    BatchId = e.BatchId
                })
                .ToList();

            // 5) التحليل الذكي للدُفعات (النسخة المعتمدة بعد الـ Many-to-Many)
            var analysis = AIBatchAnalyzer.AnalyzeAll(batches, enrollments, performances);

            // 6) إحصائيات الـ Dashboard
            var now = DateTime.Today;
            var recentBatches = _context.Batches.AsNoTracking()
                .Count(b => b.StartDate >= now.AddDays(-30));

            var totalStudents = students.Count;
            var maleStudents = students.Count(s => s.Gender == "ذكر");
            var femaleStudents = students.Count(s => s.Gender == "أنثى");
            var mixedStudents = students.Count(s => s.Gender == "مختلط"); // لو مش مستخدمين "مختلط" في Student، سيبها 0

            var topBatch = analysis.OrderByDescending(a => a.AverageScore).FirstOrDefault();
            var topAverage = topBatch?.AverageScore ?? 0;
            var topBatchName = topBatch?.BatchName ?? "غير متوفر";

            var minPassRate = analysis.Any() ? analysis.Min(a => a.PassRate) : 0;
            var weakBatchesCount = analysis.Count(a => a.PassRate == minPassRate);

            var dashboardVM = new BatchDashboardViewModel
            {
                Batches = analysis,
                TotalBatches = batches.Count,
                RecentBatches = recentBatches,
                TotalStudents = totalStudents,
                MaleStudents = maleStudents,
                FemaleStudents = femaleStudents,
                MixedStudents = mixedStudents,
                TopAverageScore = topAverage,
                TopBatchName = topBatchName,
                MinPassRate = minPassRate,
                WeakBatchesCount = weakBatchesCount
            };

            return View(dashboardVM);
        }


        public IActionResult Students(int id)
        {
            // اسم الدفعة
            var batchName = _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == id)
                .Select(b => b.Name)
                .FirstOrDefault();

            if (batchName == null) return NotFound();

            // الطلاب المنضمّين لهذه الدفعة عبر جدول الربط
            var students = (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join s in _context.Students.AsNoTracking() on e.StudentID equals s.StudentID
                where e.BatchId == id
                orderby s.FullName
                select new StudentVM // ← alias من الخطوة (1)
                {
                    StudentID = s.StudentID,
                    FullName = s.FullName,
                    Email = s.Email,
                    PhoneNumber = s.PhoneNumber,
                    Gender = s.Gender,
                    School = s.School,
                    Level = s.Level,
                    EnrollmentStatus = s.EnrollmentStatus
                }
            ).ToList();

            ViewBag.BatchName = batchName; // لو عايز تلغي ViewBag لاحقًا، سلّم الاسم داخل ViewModel صفحة
            return View(students);
        }






    }
}
