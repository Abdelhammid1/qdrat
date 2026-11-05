namespace QdratNew.Services.PartnerDispatch
{
    // APD-S1.2: دفعات «المنصة» و«الشركاء» وأهلية كل شريك لنوع الإرسال.
    public interface IDispatchAudienceService
    {
        // دفعات المعهد فقط (Branch.PartnerId == null)، غير محذوفة وغير مؤرشفة.
        // لا تُفلتر بمنهج المصدر حتى تطابق قوائم الشاشات الحالية، وتُفلتر بكورس المصدر إن وُجد.
        Task<IReadOnlyList<DispatchBatchVm>> GetPlatformBatchesAsync(DispatchSource source, CancellationToken ct = default);

        // الشركاء النشطون أو أصحاب عقد نشط، مع أهلية كل شريك ودفعاته المتوافقة مع المصدر
        Task<IReadOnlyList<PartnerEligibilityVm>> GetPartnersAsync(DispatchSource source, CancellationToken ct = default);

        // يتحقق من الطلب كاملًا: كل دفعة تخص شريكها، كل شريك مؤهل أو مُتجاوز بصلاحية وسبب،
        // لا دفعة منصة تحت «الشركاء» والعكس. صلاحية PartnerDispatch.Send مسؤولية المستدعي.
        Task<AudienceValidation> ValidateAsync(DispatchSource source, AudienceSelection selection, bool canOverride, CancellationToken ct = default);
    }
}
