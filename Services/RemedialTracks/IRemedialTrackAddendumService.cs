using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S13 (D29–D34): إدارة «ملحق المحور» من جهة الأدمن — فيديو إضافي (+ اختبار اختياري) لطلاب أمر نشر منشور.
    /// الملحق كيان مستقل: لا يغيّر <c>AxisProgress</c> ولا حالة التسجيل ولا يمنع المحور التالي إطلاقًا (D29).
    /// كل عملية تتحقق من نطاق الدفعات (<see cref="RemedialTrackBatchScope"/>) داخل الخدمة نفسها.
    /// </summary>
    public interface IRemedialTrackAddendumService
    {
        /// <summary>
        /// ينشئ الملحق وتقدّم الطلاب المستهدفين دفعة واحدة (AddRange + SaveChanges واحد). المعرّفات الغريبة ← رفض الطلب كله.
        /// عند النجاح: Data = معرّف الملحق (int).
        /// </summary>
        Task<RemedialTrackResult> CreateAsync(CreateAddendumInput input, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>إيقاف/تفعيل ملحق (Idempotent). الملحق الموقوف يختفي عن الطالب والتقارير ولا يُحذف منه شيء.</summary>
        Task<RemedialTrackResult> SetActiveAsync(int addendumId, bool active, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>
        /// ملخص متابعة كل ملاحق أمر النشر (استعلام واحد مجمّع، الأحدث أولًا). لا يتحقق من النطاق بنفسه —
        /// الكنترولر يمرّ عبر <see cref="GetPanelAsync"/> الذي يفرضه.
        /// </summary>
        Task<IReadOnlyList<AddendumTrackingRow>> GetTrackingAsync(int publicationId, CancellationToken ct = default);

        /// <summary>لوحة الملاحق في صفحة التفاصيل (مرقّمة من الخادم). null = الأمر غير موجود/خارج النطاق.</summary>
        Task<RemedialTrackAddendaPanelVm?> GetPanelAsync(int publicationId, int page, bool canManage, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>طلاب ملحق واحد (مرقّم): مشاهدة/نجاح/محاولات/آخر نشاط. null = الملحق غير موجود/خارج النطاق.</summary>
        Task<AddendumStudentsPage?> GetStudentsAsync(int addendumId, int page, RemedialTrackBatchScope scope, CancellationToken ct = default);
    }
}
