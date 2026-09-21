using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Course;
using System;
using System.Linq;
using System.Threading.Tasks;
namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class CoursesDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CoursePredictionService _predictionService;

        public CoursesDashboardController(ApplicationDbContext context, CoursePredictionService predictionService)
        {
            _context = context;
            _predictionService = predictionService;

        }

        public async Task<IActionResult> Index()
        {
            Console.WriteLine("🔍 تحليل بيانات الدورات...");

            var courses = await _context.Courses
                .Include(c => c.Branch)
                .Include(c => c.Project)
                .ToListAsync();

            var totalCourses = courses.Count;
            var activeCourses = courses.Count(c => c.IsActive);
            var inactiveCourses = totalCourses - activeCourses;
            var mostPopularCourse = courses.OrderByDescending(c => c.Id).FirstOrDefault()?.Name ?? "غير متوفر";

            // 🔹 تحليل البيانات لتوقع الاتجاهات المستقبلية
            var courseRegistrations = await _context.StudentCourses
            .GroupBy(sc => new { sc.CourseId, sc.DateEnrolled.Month })
            .Select(g => new CourseTrendViewModel
            {
                CourseId = g.Key.CourseId,
                Month = g.Key.Month,
                Registrations = g.Count()
            })
            .ToListAsync();
            // 🔹 توقع عدد التسجيلات للأشهر القادمة
            var predictions = new Dictionary<int, float>();
            for (int month = 1; month <= 12; month++)
            {
                predictions[month] = _predictionService.Predict(month);
            }

            

            var viewModel = new CourseStatisticsViewModel
            {
                TotalCourses = totalCourses,
                ActiveCourses = activeCourses,
                InactiveCourses = inactiveCourses,
                MostPopularCourse = mostPopularCourse,
                CoursePredictions = predictions,
                CourseTrends = courseRegistrations
            };

            return View(viewModel);
        }
    }
}
