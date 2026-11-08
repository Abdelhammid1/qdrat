using Microsoft.AspNetCore.Mvc;
using QdratNew.Attributes;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Interfaces;
using QdratNew.Services.OnlineLecture;
using QdratNew.ViewModels.OnlineLecture;

namespace QdratNew.Areas.Partner.Controllers
{
    /// <summary>
    /// OLC-S4.3 — «المحتوى التعليمي» في منطقة الشريك: قراءة فقط (R19). لا إنشاء ولا تعديل ولا نشر — الإدارة من الأدمن (R18).
    /// البند CanAccessEducationalContent يحكم الوصول، والقراءة مسموحة بعد انتهاء العقد حتى AccessUntilDate.
    /// كل الأفعال GET، والنطاق ActivePartnerId (المتحقَّق منه في PartnerBaseController) — لا partnerId من الطلب.
    /// </summary>
    [Area("Partner")]
    [PartnerFeature(PartnerFeatureType.EducationalContent, AllowReadAfterExpiry = true)]
    public class LearningContentController : PartnerBaseController
    {
        private readonly IPartnerLearningContentService _learning;
        private readonly IOnlineLectureFeatureService _feature;

        public LearningContentController(
            IPartnerSubscriptionService subscriptionService,
            ApplicationDbContext context,
            IPartnerLearningContentService learning,
            IOnlineLectureFeatureService feature)
            : base(subscriptionService, context)
        {
            _learning = learning;
            _feature = feature;
        }

        // GET /Partner/LearningContent
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var enabled = await _feature.IsEnabledAsync(ct);
            return View(new PartnerLearningIndexVm
            {
                FeatureEnabled = enabled,
                IsReadAfterExpiry = SubscriptionContext == null,
                Batches = enabled
                    ? (await _learning.GetBatchesAsync(ActivePartnerId, ct)).ToList()
                    : new List<PartnerLearningBatchVm>()
            });
        }

        // GET /Partner/LearningContent/Batch/5
        [HttpGet]
        public async Task<IActionResult> Batch(int id, CancellationToken ct)
        {
            if (!await _feature.IsEnabledAsync(ct))
                return NotFound();

            var vm = await _learning.GetBatchAsync(ActivePartnerId, id, ct);
            return vm is null ? NotFound() : View(vm);
        }

        // GET /Partner/LearningContent/Lecture/7
        [HttpGet]
        public async Task<IActionResult> Lecture(int id, CancellationToken ct)
        {
            if (!await _feature.IsEnabledAsync(ct))
                return NotFound();

            var vm = await _learning.GetLectureAsync(ActivePartnerId, id, ct);
            return vm is null ? NotFound() : View(vm);
        }
    }
}
