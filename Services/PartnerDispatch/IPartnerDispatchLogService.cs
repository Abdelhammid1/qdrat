using QdratNew.Entities;

namespace QdratNew.Services.PartnerDispatch
{
    // APD-S1.2: سجل ما أرسله الأدمن للشركاء (قراءة فقط بعد الكتابة)
    public interface IPartnerDispatchLogService
    {
        // يكتب داخل الـ Transaction الحالية للمستدعي (Transaction الشريك)، ولا يفتح Transaction خاصة به.
        // يضبط SentAtUtc إن لم يُضبط، ويقص العنوان والسبب لحدود الأعمدة.
        Task WriteAsync(IReadOnlyCollection<PartnerDispatchLog> entries, CancellationToken ct = default);

        IQueryable<PartnerDispatchLog> Query();
    }
}
