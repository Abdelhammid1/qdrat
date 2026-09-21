using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Interfaces;
using QdratNew.Services.PartnerHomework.ProfessionalModels;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    public class ProfessionalModelsController : PartnerBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IProfessionalModelAssignmentService _modelAssignmentService;

        public ProfessionalModelsController(
            ApplicationDbContext context,
            IProfessionalModelAssignmentService modelAssignmentService,
            IPartnerSubscriptionService subscriptionService)
            : base(subscriptionService, context)
        {
            _context = context;
            _modelAssignmentService = modelAssignmentService;
        }

        // =========================================
        // 📄 قائمة النماذج الاحترافية
        // =========================================
        public IActionResult Index()
        {
            int partnerId = ActivePartnerId;
            int? periodId = ActiveSubscriptionPeriodId;

            var modelsQuery = _context.ProfessionalModels
                .Where(m => !m.IsArchived && m.ModelType == ProfessionalModelType.Homework)
                .AsQueryable();

            modelsQuery = modelsQuery.Where(m =>
                // 1️⃣ نموذج عام
                m.IsGlobal

                // 2️⃣ نموذج مخصص للشريك
                || _context.ProfessionalModelPartners
                    .Any(p =>
                        p.ProfessionalModelId == m.Id &&
                        p.PartnerId == partnerId)

                // 3️⃣ نموذج مخصص لفترة اشتراك (في حال وجود فترة نشطة)
                || (periodId.HasValue &&
                    _context.ProfessionalModelSubscriptionPeriods
                        .Any(sp =>
                            sp.ProfessionalModelId == m.Id &&
                            sp.PartnerSubscriptionPeriodId == periodId.Value))
            );

            var models = modelsQuery
                .Include(m => m.Questions)
                    .ThenInclude(q => q.Question)
                .OrderByDescending(m => m.CreatedAt)
                .ToList();

            return View(models);
        }




        // =========================================
        // 👁️ معاينة النموذج
        // =========================================
        public IActionResult Preview(int id)
        {
            var model = _context.ProfessionalModels
                .Include(m => m.Questions)
                .ThenInclude(q => q.Question)
                .FirstOrDefault(m => m.Id == id && !m.IsArchived);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // =========================================
        // ✅ إرسال النموذج كما هو
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Send(int modelId)
        {
            var modelIsAvailable = _context.ProfessionalModels
                .Any(m => m.Id == modelId && !m.IsArchived);

            if (!modelIsAvailable)
            {
                TempData["Error"] = "النموذج الاحترافي غير متاح أو مؤرشف.";
                return RedirectToAction(nameof(Index));
            }

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
        public IActionResult SendCustomized(
            int modelId,
            List<Guid> questionIds)
        {
            _modelAssignmentService.SendModelToStudents(
                modelId,
                ActivePartnerId,
                ActiveSubscriptionPeriodId,
                questionIds
            );

            TempData["Success"] = "تم إرسال النموذج بتكوين مخصص.";
            return RedirectToAction(nameof(Index));
        }
    }
}
