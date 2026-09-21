using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Students;
using System.Linq;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class StudentCourseEnrollmentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentCourseEnrollmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new StudentCourseEnrollmentRegisterViewModel
            {
                Students = _context.Students.Select(s => new SelectListItem
                {
                    Value = s.StudentID.ToString(),
                    Text = s.FullName
                }).ToList(),
                Courses = _context.Courses.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                }).ToList()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(StudentCourseEnrollmentRegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Students = _context.Students.Select(s => new SelectListItem
                {
                    Value = s.StudentID.ToString(),
                    Text = s.FullName
                }).ToList();
                model.Courses = _context.Courses.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                }).ToList();
                return View(model);
            }

            var enrollment = new StudentCourseEnrollment
            {
                StudentId = model.StudentId,
                CourseId = model.CourseId,
                EnrollmentDate = DateTime.UtcNow
            };

            _context.StudentCourseEnrollments.Add(enrollment);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "✅ تم تسجيل الطالب في الدورة بنجاح!";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Index()
        {
            var enrollments = _context.StudentCourseEnrollments
                .Select(e => new
                {
                    StudentName = e.Student.FullName,
                    CourseName = e.Course.Name,
                    e.EnrollmentDate
                }).ToList();

            return View(enrollments);
        }
    }
}
