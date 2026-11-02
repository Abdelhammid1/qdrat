using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QdratNew.Attributes;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Interfaces;
using QdratNew.ViewModels.Partner;
using System.Linq;
using System.Security.Claims;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    [Authorize(Policy = "PartnerOnly")] // PPL-S1.2: يورَّث لكل Controllers الشريك
    public abstract class PartnerBaseController : Controller
    {
        private const string PartnerLinkCheckItemKey = "PPL.ActivePartnerLinked";

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

        // PPL-S1.2: الشريك النشط في الـ Session يجب أن يخص المستخدم الحالي
        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            if (ActivePartnerId > 0 && !await IsActivePartnerLinkedAsync())
            {
                HttpContext.Session.Remove("ActivePartnerId");
                context.Result = RedirectToAction("Select", "PartnerContext");
                return;
            }

            await base.OnActionExecutionAsync(context, next);
        }

        private async Task<bool> IsActivePartnerLinkedAsync()
        {
            if (HttpContext.Items[PartnerLinkCheckItemKey] is bool cached)
                return cached;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var partnerId = ActivePartnerId;

            var linked = await _context.UserPartners
                .AnyAsync(up => up.UserId == userId && up.PartnerId == partnerId);

            HttpContext.Items[PartnerLinkCheckItemKey] = linked;
            return linked;
        }

        private bool CanReadAfterExpiry(ActionExecutingContext context)
        {
            var feature = context.ActionDescriptor.EndpointMetadata
                .OfType<PartnerFeatureAttribute>()
                .LastOrDefault();

            return feature?.AllowReadAfterExpiry == true &&
                   _subscriptionService.CanReadFeatureAfterExpiry(ActivePartnerId, feature.Feature);
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
                (controller == "Home" && action == "AccessDenied") || // PPL-S1.5
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
                // PPL-S6: تقارير AllowReadAfterExpiry تبقى متاحة حتى AccessUntilDate، وPartnerFeatureFilter يكمل الفحص
                if (controller != "Students" && controller != "Batches" && !CanReadAfterExpiry(context))
                {
                    context.Result = RedirectToAction("NoActiveSubscription", "Home");
                    return;
                }
            }

            // =====================================================
            // ViewBag
            // =====================================================
            // PPL-S1.5: صفحات Students/Batches تعمل بدون اشتراك نشط
            ViewBag.CanUseProfessionalModels =
                SubscriptionContext?.CanUseProfessionalModels ?? false;

            base.OnActionExecuting(context);
        }


    }
}
