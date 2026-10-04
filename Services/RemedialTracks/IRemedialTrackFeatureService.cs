namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S7.4: مفتاح تعطيل الميزة (SystemSettings: RemedialTrack.Enabled، الافتراضي true).
    /// التعطيل يخفي قائمة الطالب ويُرجع 404 لأكشنات الطالب دون المساس بالبيانات؛ لوحات الأدمن والتقارير تبقى متاحة.
    /// </summary>
    public interface IRemedialTrackFeatureService
    {
        Task<bool> IsEnabledAsync(CancellationToken ct = default);
    }
}
