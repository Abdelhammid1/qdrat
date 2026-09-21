using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace QdratNew.Areas.Identity.Pages.Account
{
    [Authorize]
    public class SwitchRoleModel : PageModel
    {
        public IActionResult OnPost(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return Redirect("/");

            HttpContext.Session.SetString("ActiveRole", role);

            return role switch
            {
                // 🔵 الموظف — لوحة تشغيلية مستقلة
                "Employee"
                    => Redirect("/Admin/EmployeeDashboard"),

                // 🔵 أدوار الإدارة
                "Admin" or "SuperAdmin" or "Owner" or "Developer" or "DataEntry"
                    => Redirect("/Admin/AdminOperationsDashboard"),

                // 🟢 جميع المدربين (منصة + شريك)
                "Instructor" or "PartnerInstructor"
                    => Redirect("/Instructors/InstructorDashboard"),

                // 🟡 الطلاب
                "Student"
                    => Redirect("/Students/Dashboard"),

                // 🟣 الشريك فقط
                "Partner" or "PartnerAdmin"
                    => Redirect("/Partner"),

                // 🟤 ولي الأمر
                "Parent"
                    => Redirect("/Parents/Dashboard"),

                _ => Redirect("/")
            };
        }
    }
}
