using Microsoft.Extensions.Primitives;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>
    /// OLC-S5.1 — إشارة إبطال كاش رابط «المحتوى التعليمي» في قائمة الطالب (<c>LearningContentMenuViewComponent</c>).
    /// كل مدخل كاش يرتبط بالرمز الحالي؛ <see cref="Invalidate"/> يُسقط كل المدخلات فورًا (لكل الطلاب) عند تغيير يمس الأهلية:
    /// تفعيل/إلغاء دفعة أونلاين، أو إنشاء/تعديل عقد شريك (بند المحتوى التعليمي). بقية التغييرات (التسجيلات، حالة الطالب،
    /// انتهاء العقد بمرور اليوم) يغطيها عمر الكاش القصير.
    /// ثابتة (Static) لأن الكاش نفسه في ذاكرة العملية (IMemoryCache) — نفس النطاق.
    /// </summary>
    public static class OnlineLearningMenuCacheSignal
    {
        private static CancellationTokenSource _source = new();

        /// <summary>الرمز الحالي — يُضاف لكل مدخل كاش بـ <c>AddExpirationToken</c>.</summary>
        public static IChangeToken Token => new CancellationChangeToken(Volatile.Read(ref _source).Token);

        /// <summary>يُبطل كل مدخلات رابط القائمة الحالية.</summary>
        public static void Invalidate()
        {
            // لا Dispose: قد يقرأ طلب متزامن الرمز القديم للتو (CancellationTokenSource بلا مؤقت لا يحتاج تحريرًا)
            var previous = Interlocked.Exchange(ref _source, new CancellationTokenSource());
            previous.Cancel();
        }
    }
}
