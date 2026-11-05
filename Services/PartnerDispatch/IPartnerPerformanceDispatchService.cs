namespace QdratNew.Services.PartnerDispatch
{
    // APD-S4.1: نسخة مستقلة من قالب مؤشر الأداء لكل مدرسة على دفعاتها المختارة:
    // Transaction مستقلة لكل مدرسة، وفشل مدرسة لا يلغي غيرها.
    public interface IPartnerPerformanceDispatchService
    {
        // targets ناتج IDispatchAudienceService.ValidateAsync
        Task<PartnerDispatchOutcome> DispatchAsync(int templateExamId, IReadOnlyList<PartnerDispatchTarget> targets, DispatchActor actor, CancellationToken ct = default);
    }
}
