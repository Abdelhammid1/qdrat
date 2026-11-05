using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Security.AdminPermissions;
using QdratNew.ViewModels.PartnerDispatch;

namespace QdratNew.Services.PartnerDispatch
{
    // APD-S1.4: يجمع صلاحيات PartnerDispatch مع بيانات الجمهور لبناء الـ Picker في أي شاشة إرسال.
    public interface IDispatchAudiencePickerBuilder
    {
        // مفتاح التعطيل PartnerDispatch.Enabled = false يجعل Send مرفوضًا للجميع بلا كاش
        Task<bool> IsEnabledAsync(CancellationToken ct = default);

        Task<bool> CanSendAsync(ClaimsPrincipal user);
        Task<bool> CanOverrideAsync(ClaimsPrincipal user);

        // includePartners: يُعدّ قائمة الشركاء من الخادم (لإعادة عرض اختيار سابق) بدل تحميلها بالـ AJAX
        Task<DispatchAudiencePickerVm> BuildAsync(ClaimsPrincipal user, DispatchSource source, AudienceSelection? selection = null, bool includePartners = false, CancellationToken ct = default);

        // يفترض أن المستدعي تحقق من صلاحية Send
        Task<DispatchAudiencePartnersVm> BuildPartnersAsync(ClaimsPrincipal user, DispatchSource source, AudienceSelection? selection = null, CancellationToken ct = default);
    }

    public class DispatchAudiencePickerBuilder : IDispatchAudiencePickerBuilder
    {
        public const string EnabledSettingKey = "PartnerDispatch.Enabled";

        private readonly IDispatchAudienceService _audience;
        private readonly IAuthorizationService _authorization;
        private readonly ApplicationDbContext _context;

        public DispatchAudiencePickerBuilder(IDispatchAudienceService audience, IAuthorizationService authorization, ApplicationDbContext context)
        {
            _audience = audience;
            _authorization = authorization;
            _context = context;
        }

        // فقط القيمة الصريحة false تعطّل، كمفتاح PPM
        public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
            => !await _context.SystemSettings.AsNoTracking().AnyAsync(s => s.Key == EnabledSettingKey && s.Value == "false", ct);

        public async Task<bool> CanSendAsync(ClaimsPrincipal user)
            => await IsEnabledAsync() &&
               (await _authorization.AuthorizeAsync(user, AdminPermissionPolicies.PartnerDispatch_Send)).Succeeded;

        public async Task<bool> CanOverrideAsync(ClaimsPrincipal user)
            => (await _authorization.AuthorizeAsync(user, AdminPermissionPolicies.PartnerDispatch_Override)).Succeeded;

        public async Task<DispatchAudiencePickerVm> BuildAsync(ClaimsPrincipal user, DispatchSource source, AudienceSelection? selection = null, bool includePartners = false, CancellationToken ct = default)
        {
            var canSend = await CanSendAsync(user);
            selection ??= new AudienceSelection();

            // بلا صلاحية Send لا يُعرض اختيار الشركاء حتى لو وصل في الطلب
            if (!canSend)
            {
                selection = new AudienceSelection
                {
                    Audience = DispatchAudience.Platform,
                    PlatformBatchIds = selection.PlatformBatchIds ?? new List<int>()
                };
            }

            return new DispatchAudiencePickerVm
            {
                Source = source,
                PlatformBatches = await _audience.GetPlatformBatchesAsync(source, ct),
                Selection = selection,
                CanSendToPartners = canSend,
                Partners = canSend && includePartners
                    ? await BuildPartnersAsync(user, source, selection, ct)
                    : null
            };
        }

        public async Task<DispatchAudiencePartnersVm> BuildPartnersAsync(ClaimsPrincipal user, DispatchSource source, AudienceSelection? selection = null, CancellationToken ct = default)
            => new()
            {
                Partners = await _audience.GetPartnersAsync(source, ct),
                CanOverride = await CanOverrideAsync(user),
                Selection = selection ?? new AudienceSelection()
            };
    }
}
