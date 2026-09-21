using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Interfaces;
using QdratNew.ViewModels.Partner;
using System.Linq;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    public class CoursesController : PartnerBaseController
    {
        private readonly ApplicationDbContext _context;

        public CoursesController(
       ApplicationDbContext context,
       IPartnerSubscriptionService subscriptionService)
       : base(subscriptionService, context)
        {
            _context = context;
        }

        // =====================================================
        // 📘 الدورات المتعاقد عليها (قراءة فقط)
        // =====================================================
        public IActionResult Index()
        {
            var partnerId = ActivePartnerId;

            // 🟢 الدورات المسموح بها من سياق العقد
            var allowedCourses = AllowedCourses;

            // 🟢 جلب جميع الدفعات الخاصة بالشريك فقط (بدون Contains)
            var partnerBatches = _context.Batches
                .Where(b => b.Branch.PartnerId == partnerId)
                .Select(b => new
                {
                    b.CourseId
                })
                .ToList();

            // 🟢 حساب عدد الدفعات لكل دورة داخل الذاكرة
            var batchesCount = partnerBatches
                .GroupBy(b => b.CourseId)
                .Select(g => new
                {
                    CourseId = g.Key,
                    Count = g.Count()
                })
                .ToList();

            // 🟢 بناء الـ ViewModel النهائي
            var model = allowedCourses
                .Select(c => new PartnerCourseListViewModel
                {
                    CourseId = c.CourseId,
                    CourseName = c.CourseName,
                    CanUsePlatformQuestionBank = c.CanUsePlatformQuestionBank,
                    BatchesCount = batchesCount
                        .FirstOrDefault(x => x.CourseId == c.CourseId)?.Count ?? 0
                })
                .OrderBy(x => x.CourseName)
                .ToList();

            return View(model);
        }

    }
}
