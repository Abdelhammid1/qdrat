using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Interfaces;
using QdratNew.ViewModels.Partner;
using System;
using System.Linq;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    [Authorize(Policy = "PartnerOnly")]
    public class HomeController : PartnerBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
         ApplicationDbContext context,
         UserManager<ApplicationUser> userManager,
         IPartnerSubscriptionService subscriptionService)
         : base(subscriptionService, context)
        {
            _context = context;
            _userManager = userManager;
        }

        // ===============================
        // 🛡️ الحارس (استثناء اختيار الشريك)
        // ===============================
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionDescriptor.RouteValues["controller"] == "Partners")
                return;

            base.OnActionExecuting(context);
        }

        // ===============================
        // 📊 لوحة الشريك
        // ===============================
        public IActionResult Index()
        {
            var partnerId = ActivePartnerId;

            var partner = _context.Partners
                .Where(p => p.Id == partnerId)
                .Select(p => new
                {
                    p.Name,
                    p.LogoPath,
                    p.PartnershipEnd
                })
                .FirstOrDefault();

            if (partner == null)
            {
                return RedirectToAction(
                    "SelectPartner",
                    "Partners",
                    new { area = "Partner" }
                );
            }

            var today = DateTime.Today;

            // عدد الطلاب في الفترة النشطة فقط
            var studentsCount = _context.Students
                .Count(s => s.PartnerSubscriptionPeriodId == ActiveSubscriptionPeriodId);

            // عدد الدفعات (قديمة – نُبقيها مؤقتًا)
            var batchesCount =
                (from bt in _context.Batches
                 join b in _context.Branches on bt.BranchId equals b.Id
                 where b.PartnerId == partnerId
                 select bt.Id)
                .Count();

            // ✔ الدورات من العقد (المصدر الصحيح)
            var coursesCount = AllowedCourses.Count;

            var model = new PartnerDashboardViewModel
            {
                PartnerName = partner.Name,
                LogoUrl = partner.LogoPath,
                IsActive = partner.PartnershipEnd >= today,
                DaysRemaining = (partner.PartnershipEnd - today).Days,

                StudentsCount = studentsCount,
                BatchesCount = batchesCount,
                CoursesCount = coursesCount,

                // جديد (للاستخدام لاحقًا في الـ View)
                Courses = AllowedCourses
            };

            return View(model);
        }


      
            public IActionResult NoActiveSubscription()
            {
                return View();
            }
       








    }
}
