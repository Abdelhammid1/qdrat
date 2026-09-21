using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Students;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class StudentsDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentsDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? branchId, int? batchId)
        {
            var studentsQuery = _context.Students
    .Include(s => s.Branch)
    .Include(s => s.BatchEnrollments)
        .ThenInclude(e => e.Batch) // للوصول لبيانات الدفعة
    .Include(s => s.StudentPerformances)
    .Include(s => s.StudyPlans)
    .AsQueryable();


            if (branchId.HasValue)
                studentsQuery = studentsQuery.Where(s => s.BranchId == branchId);
            if (batchId.HasValue)
            {
                studentsQuery = studentsQuery
                    .Where(s => s.BatchEnrollments.Any(e => e.BatchId == batchId));
            }


            var students = await studentsQuery.ToListAsync();

            // تحليل الطلاب المتعثرين 👇
            var atRiskStudents = students
                .Where(s =>
                    s.StudentPerformances.Count(p => p.Score < 50) >= 2 || // أقل من 50 مرتين أو أكثر
                    !s.StudyPlans.Any()
                )
                .Select(s => s.FullName)
                .Take(5)
                .ToList();

            var viewModel = new DashboardStudentStatsViewModel
            {
                SelectedBranchId = branchId,
                SelectedBatchId = batchId,

                Branches = await _context.Branches.Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToListAsync(),

                Batches = await _context.Batches.Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToListAsync(),

                TotalStudents = students.Count,
                ActiveStudents = students.Count(s => s.EnrollmentStatus == "نشط"),
                InactiveStudents = students.Count(s => s.EnrollmentStatus != "نشط"),
                MaleStudents = students.Count(s => s.Gender == "ذكر"),
                FemaleStudents = students.Count(s => s.Gender == "أنثى"),

                StudentsByBranch = students
                    .GroupBy(s => s.Branch?.Name ?? "غير محدد")
                    .ToDictionary(g => g.Key, g => g.Count()),

                StudentsByLevel = students
                    .GroupBy(s => s.Level ?? "غير محدد")
                    .ToDictionary(g => g.Key, g => g.Count()),

                RegistrationsByMonth = students
    .GroupBy(s => s.RegistrationDate.ToString("yyyy-MM"))
    .OrderBy(g => g.Key)
    .ToDictionary(g => g.Key, g => g.Count()),

                AtRiskStudents = atRiskStudents
            };

            return View(viewModel);
        }



        public async Task<IActionResult> List(string status)
        {
            var studentsQuery = _context.Students
      .Include(s => s.Branch)
      .Include(s => s.BatchEnrollments)
          .ThenInclude(e => e.Batch)
      .Include(s => s.StudentPerformances)
      .Include(s => s.StudyPlans)
      .AsQueryable();

            ViewBag.StatusTitle = "كل الطلاب";

            switch (status)
            {
                case "active":
                    studentsQuery = studentsQuery.Where(s => s.EnrollmentStatus == "نشط");
                    ViewBag.StatusTitle = "الطلاب النشطين";
                    break;
                case "inactive":
                    studentsQuery = studentsQuery.Where(s => s.EnrollmentStatus != "نشط");
                    ViewBag.StatusTitle = "الطلاب الموقوفين / غير نشطين";
                    break;
                case "male":
                    studentsQuery = studentsQuery.Where(s => s.Gender == "ذكر");
                    ViewBag.StatusTitle = "الطلاب الذكور";
                    break;
                case "female":
                    studentsQuery = studentsQuery.Where(s => s.Gender == "أنثى");
                    ViewBag.StatusTitle = "الطالبات الإناث";
                    break;
            }

            var students = await studentsQuery
                .Select(s => new StudentViewModel
                {
                    StudentID = s.StudentID,
                    FullName = s.FullName,
                    Gender = s.Gender,
                    Level = s.Level,
                    BranchId = s.BranchId,
                    BatchId = s.Branch.Id,
                    PhoneNumber = s.PhoneNumber,
                    WhatsAppNumber = s.WhatsAppNumber,
                    Email = s.Email,
                    EnrollmentStatus = s.EnrollmentStatus
                }).ToListAsync();

            return View(students);
        }



    }
}
