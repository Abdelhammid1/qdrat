using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Services.PartnerDispatch;

namespace QdratNew.Areas.Admin.Controllers
{
    // APD-S1.4: بيانات Partial اختيار جمهور الإرسال المشترك بين شاشات الإرسال الخمس
    [Area("Admin")]
    [Authorize(Policy = "AdminArea")]
    public class PartnerDispatchController : Controller
    {
        private readonly IDispatchAudiencePickerBuilder _picker;

        public PartnerDispatchController(IDispatchAudiencePickerBuilder picker)
        {
            _picker = picker;
        }

        // قائمة المدارس الشريكة وأهليتها ودفعاتها المتوافقة مع المصدر، كـ HTML جاهز للحقن في الـ Picker.
        // منهج النموذج يُقرأ من القاعدة داخل الخدمة، فلا يُعتمد على curriculumId القادم مع النموذج.
        [HttpGet]
        [AdminPermission("PartnerDispatch", "Send")]
        public async Task<IActionResult> AudienceOptions(PartnerDispatchKind kind, int? modelId, int? curriculumId, int? courseId, CancellationToken ct)
        {
            if (!Enum.IsDefined(kind))
                return BadRequest();

            if (!await _picker.IsEnabledAsync(ct))
                return NotFound();

            var source = new DispatchSource(kind, modelId, curriculumId, courseId);
            var vm = await _picker.BuildPartnersAsync(User, source, null, ct);

            return PartialView("_DispatchAudiencePartners", vm);
        }
    }
}
