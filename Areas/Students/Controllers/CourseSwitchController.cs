using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Students.Abstractions;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class CourseSwitchController : Controller
    {
        private readonly IStudentCourseContext _courseCtx;

        public CourseSwitchController(IStudentCourseContext courseCtx)
        {
            _courseCtx = courseCtx;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Set(int courseId, int batchId, string? returnUrl = null)
        {
            _courseCtx.SetSelection(courseId, batchId);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Dashboard", new { area = "Students" });
        }




    }
}
