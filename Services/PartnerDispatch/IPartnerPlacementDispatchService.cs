namespace QdratNew.Services.PartnerDispatch
{
    // APD-S3.1: إرسال فوري للطلاب، أو مسودة تظهر في منطقة المدرسة لتراجعها وترسلها
    public enum PlacementPartnerDelivery
    {
        SendNow = 1,
        Draft = 2
    }

    public sealed class SendPlacementToPartnersRequest
    {
        public int ModelId { get; init; }
        public int CourseId { get; init; }
        public int DurationMinutes { get; init; }
        public PlacementPartnerDelivery Delivery { get; init; } = PlacementPartnerDelivery.SendNow;
    }

    // APD-S3.1: اختبار تحديد مستوى مستقل لكل مدرسة على دفعاتها المختارة:
    // Transaction مستقلة لكل مدرسة، وفشل مدرسة لا يلغي غيرها.
    public interface IPartnerPlacementDispatchService
    {
        // targets ناتج IDispatchAudienceService.ValidateAsync
        Task<PartnerDispatchOutcome> DispatchAsync(IReadOnlyList<PartnerDispatchTarget> targets, SendPlacementToPartnersRequest request, DispatchActor actor, CancellationToken ct = default);
    }
}
