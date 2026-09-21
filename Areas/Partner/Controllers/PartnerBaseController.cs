using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Interfaces;
using QdratNew.ViewModels.Partner;
using System.Linq;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    public abstract class PartnerBaseController : Controller
    {
        protected readonly IPartnerSubscriptionService _subscriptionService;
        protected readonly ApplicationDbContext _context;

        protected int ActivePartnerId =>
            HttpContext.Session.GetInt32("ActivePartnerId") ?? 0;

        protected PartnerSubscriptionContext? SubscriptionContext { get; private set; }


        // ✅ فلتر موحد للشريك (يستخدم في كل الكنترولرات)
        protected IQueryable<Student> PartnerStudents =>
            _context.Students.Where(s => s.Branch.PartnerId == ActivePartnerId);
        // ✅ الطلاب النشطين فقط
        protected IQueryable<Student> ActiveStudents =>
            _context.Students.Where(s =>
                s.Branch.PartnerId == ActivePartnerId &&
                s.IsActiveForLearning);

        // ✅ الطلاب المؤرشفين
        protected IQueryable<Student> ArchivedStudents =>
            _context.Students.Where(s =>
                s.Branch.PartnerId == ActivePartnerId &&
                !s.IsActiveForLearning);


        protected int ActiveSubscriptionPeriodId =>
            SubscriptionContext?.ActivePeriodId ?? 0;

        protected List<PartnerCourseContext> AllowedCourses =>
            SubscriptionContext?.Courses ?? new();

        protected PartnerBaseController(
            IPartnerSubscriptionService subscriptionService,
            ApplicationDbContext context)
        {
            _subscriptionService = subscriptionService;
            _context = context;
        }
        protected IActionResult RequirePermission(bool condition)
        {
            if (!condition)
                return RedirectToAction("AccessDenied", "Home");

            return null;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var controller = context.ActionDescriptor.RouteValues["controller"];
            var action = context.ActionDescriptor.RouteValues["action"];

            // =========================================
            // ✅ استثناءات مهمة (منع الـ Redirect Loop)
            // =========================================
            if (
                (controller == "Home" && action == "NoActiveSubscription") ||
                (controller == "PartnerContext") // صفحة اختيار الشريك
            )
            {
                base.OnActionExecuting(context);
                return;
            }

            // 🔴 لا يوجد شريك نشط
            if (ActivePartnerId <= 0)
            {
                context.Result = RedirectToAction("Select", "PartnerContext");
                return;
            }

            // =====================================================
            // 🟢 ضمان وجود فرع واحد على الأقل (فرع افتراضي)
            // =====================================================
            var hasBranch = _context.Branches
                .Any(b => b.PartnerId == ActivePartnerId);

            if (!hasBranch)
            {
                var partner = _context.Partners.Find(ActivePartnerId);

                if (partner != null)
                {
                    var defaultBranch = new Branch
                    {
                        PartnerId = partner.Id,
                        Name = $"{partner.Name} - الفرع الرئيسي",
                        Country = "غير محدد",
                        City = "غير محدد",
                        State = "غير محدد",
                        Location = "فرع افتراضي تم إنشاؤه تلقائيًا"
                    };

                    _context.Branches.Add(defaultBranch);
                    _context.SaveChanges();
                }
            }

            // =====================================================
            // 🟢 تحميل سياق الاشتراك
            // =====================================================
            SubscriptionContext =
                _subscriptionService.GetActiveContext(ActivePartnerId);
            if (SubscriptionContext == null)
            {
                // ❗ لا تمنع الوصول لكل الصفحات
                // اسمح ببعض الصفحات الأساسية
                if (controller != "Students" && controller != "Batches")
                {
                    context.Result = RedirectToAction("NoActiveSubscription", "Home");
                    return;
                }
            }

            // =====================================================
            // ViewBag
            // =====================================================
            ViewBag.CanUseProfessionalModels =
                SubscriptionContext.CanUseProfessionalModels;

            base.OnActionExecuting(context);
        }


    }
}
