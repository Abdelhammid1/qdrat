using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Attributes;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Interfaces;
using QdratNew.Services.PartnerHomework.ProfessionalModels;
using QdratNew.Services.ProfessionalModels;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    [PartnerFeature(PartnerFeatureType.ProfessionalModels)] // PPL-S1.4
    public class ProfessionalModelsController : PartnerBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IProfessionalModelAssignmentService _modelAssignmentService;
        private readonly IProfessionalModelAvailabilityService _availabilityService;

        public ProfessionalModelsController(
            ApplicationDbContext context,
            IProfessionalModelAssignmentService modelAssignmentService,
            IProfessionalModelAvailabilityService availabilityService,
            IPartnerSubscriptionService subscriptionService)
            : base(subscriptionService, context)
        {
            _context = context;
            _modelAssignmentService = modelAssignmentService;
            _availabilityService = availabilityService;
        }

        // =========================================
        // 📄 قائمة النماذج الاحترافية
        // =========================================
        public async Task<IActionResult> Index()
        {
            var models = await _availabilityService
                .AvailableForPartner(ActivePartnerId, ProfessionalModelType.Homework)
                .Include(m => m.Questions)
                    .ThenInclude(q => q.Question)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            return View(models);
        }




        // =========================================
        // 👁️ معاينة النموذج
        // =========================================
        public async Task<IActionResult> Preview(int id)
        {
            if (!await IsModelAvailableForPartnerAsync(id))
                return NotFound();

            var model = await _context.ProfessionalModels
                .Include(m => m.Questions)
                .ThenInclude(q => q.Question)
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsArchived);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // =========================================
        // ✅ إرسال النموذج كما هو
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(int modelId)
        {
            var modelIsAvailable = await _context.ProfessionalModels
                .AnyAsync(m => m.Id == modelId && !m.IsArchived);

            if (!modelIsAvailable)
            {
                TempData["Error"] = "النموذج الاحترافي غير متاح أو مؤرشف.";
                return RedirectToAction(nameof(Index));
            }

            if (!await IsModelAvailableForPartnerAsync(modelId))
                return NotFound();

            _modelAssignmentService.SendModelToStudents(
                modelId,
                ActivePartnerId,
                ActiveSubscriptionPeriodId
            );

            TempData["Success"] = "تم إرسال النموذج للطلاب بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // =========================================
        // ⚙️ إرسال مخصص (إضافة / استبدال أسئلة)
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendCustomized(
            int modelId,
            List<Guid> questionIds)
        {
            if (!await IsModelAvailableForPartnerAsync(modelId))
                return NotFound();

            _modelAssignmentService.SendModelToStudents(
                modelId,
                ActivePartnerId,
                ActiveSubscriptionPeriodId,
                questionIds
            );

            TempData["Success"] = "تم إرسال النموذج بتكوين مخصص.";
            return RedirectToAction(nameof(Index));
        }

        // =========================================
        // 🔒 PPL-S1.6: نفس قواعد الإتاحة في Index
        // =========================================
        private Task<bool> IsModelAvailableForPartnerAsync(int modelId)
            => _availabilityService.IsAvailableForPartnerAsync(modelId, ActivePartnerId, null);
    }
}
