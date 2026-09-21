using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace QdratNew.Filters
{
    public class StudentActiveCourseFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var http = context.HttpContext;

            if (!http.Session.TryGetValue("ActiveStudentCourseId", out _))
            {
                context.Result = new RedirectToActionResult(
                    "SelectCourse",
                    "StudentCourses",
                    new { area = "Students" }
                );
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
