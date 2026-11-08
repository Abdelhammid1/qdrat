namespace QdratNew.Services.OnlineLecture
{
    /// <summary>
    /// OLC-S1.3 (R11): مفتاح الميزة SystemSettings: OnlineLectureContent.Enabled — الافتراضي false حتى الإطلاق.
    /// بخلاف RemedialTrack.Enabled: الإعداد الغائب أو غير الصالح = مغلقة؛ فقط القيمة الصريحة true تفعّل الميزة.
    /// المغلق يخفي قسم «المحتوى التعليمي» ويُرجع 404 لأكشنات الطالب دون المساس بالبيانات.
    /// </summary>
    public interface IOnlineLectureFeatureService
    {
        Task<bool> IsEnabledAsync(CancellationToken ct = default);
    }
}
