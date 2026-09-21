using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace QdratNew.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            var loginUrl = "/LMS/login";

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                loginUrl += "?returnUrl=" + Uri.EscapeDataString(returnUrl);
            }

            return LocalRedirect(loginUrl);
        }

        [Authorize]
        [HttpPost]
        public IActionResult SwitchRole([FromBody] string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return Json(new { redirectUrl = "/" });

            HttpContext.Session.SetString("ActiveRole", role);

            string redirectUrl = role switch
            {
                "Admin" or "SuperAdmin" or "Owner" or "Developer" or "Employee" or "DataEntry"
                    => "/Admin/AdminOperationsDashboard",

                "Instructor"
                    => "/Instructors/InstructorDashboard",

                "Student"
                    => "/Students/Dashboard",

                "Partner" or "PartnerAdmin" or "PartnerInstructor"
                    => "/Partner",

                "Parent"
                    => "/Parents/Dashboard",

                _ => "/"
            };

            return Json(new { redirectUrl });
        }


    }
}
