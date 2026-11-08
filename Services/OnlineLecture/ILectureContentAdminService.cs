using QdratNew.Enums;
using QdratNew.ViewModels.OnlineLecture;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>نتيجة عمليات الأدمن على المحتوى. <see cref="Id"/> = معرّف المحتوى عند الإنشاء.</summary>
    public sealed record LectureContentResult(bool Success, string Message, int? Id = null, bool NotFound = false)
    {
        public static LectureContentResult Ok(string message, int? id = null) => new(true, message, id);
        public static LectureContentResult Fail(string message, int? id = null) => new(false, message, id);
        public static LectureContentResult Missing(string message = "العنصر غير موجود.") => new(false, message, null, true);
    }

    /// <summary>
    /// OLC-S2 — إدارة المحتوى التعليمي أونلاين من الأدمن (R10: الأدمن فقط بصلاحية صريحة).
    /// S2: تفعيل الدفعة الأونلاين + إنشاء المسودة (R1) + البيانات الأساسية والوضع والفيديو الكامل (R8).
    /// S3: الفصول (R4/R7) وفيديوهات المؤشرات (R5/R6) ودورة النشر Draft → Published → Hidden.
    /// </summary>
    public interface ILectureContentAdminService
    {
        Task<LectureContentEditVm?> GetForLectureAsync(int lectureId, CancellationToken ct = default);

        Task<int?> GetLectureIdOfContentAsync(int contentId, CancellationToken ct = default);

        /// <summary>R1: لا يُنشأ محتوى إلا لمحاضرة دفعتها أونلاين. Idempotent: إن وُجد محتوى يُعاد معرّفه؛ المحذوف ناعمًا يُعاد تفعيله كمسودة.</summary>
        Task<LectureContentResult> CreateAsync(int lectureId, LectureContentCreateVm vm, string userId, CancellationToken ct = default);

        /// <summary>R8 للرابط + قواعد تغيير الوضع (تأكيد وحذف ناعم للعناصر، ولا تغيير للوضع أثناء النشر).</summary>
        Task<LectureContentResult> UpdateAsync(int id, LectureContentUpdateVm vm, string userId, CancellationToken ct = default);

        Task<BatchOnlineLearningSummaryVm?> GetBatchOnlineSummaryAsync(int batchId, CancellationToken ct = default);

        /// <summary>OLC-S2.1: تفعيل/إلغاء التعليم أونلاين للدفعة. الإلغاء لا يحذف أي محتوى أو تقدّم.</summary>
        Task<LectureContentResult> SetBatchOnlineLearningAsync(int batchId, bool enable, CancellationToken ct = default);

        /// <summary>حالة المحتوى لكل محاضرة في الدفعة (LectureId ← Status) — استعلام واحد لقائمة المحاضرات.</summary>
        Task<IReadOnlyDictionary<int, LectureContentStatus>> GetStatusesForBatchAsync(int batchId, CancellationToken ct = default);

        /// <summary>هل للمحاضرة صف محتوى (ولو محذوفًا ناعمًا)؟ — الحذف الفعلي للمحاضرة يفشل بسبب Restrict.</summary>
        Task<bool> LectureHasContentAsync(int lectureId, CancellationToken ct = default);

        // ═══════════════ OLC-S3 ═══════════════

        /// <summary>مؤشرات محور المحاضرة (Lesson.SectionId = Lecture.SectionId) — اقتراح الفصول وإضافة الفيديوهات.</summary>
        Task<IReadOnlyList<LessonOptionVm>> SuggestLessonsAsync(int lectureId, CancellationToken ct = default);

        /// <summary>فيديوهات المؤشرات النشطة لكل مؤشرات محور المحاضرة (استعلام واحد) — غير الصالحة وفق R8 تُستبعد مع تحذير في السجل.</summary>
        Task<IReadOnlyList<IndicatorVideoOptionVm>> GetIndicatorVideoOptionsAsync(int lectureId, CancellationToken ct = default);

        /// <summary>OLC-S3.1 — يستبدل فصول FullVideo بالكامل (R4 + R7). الصفوف بلا Include تُزال (حذف ناعم).</summary>
        Task<LectureContentResult> SetChaptersAsync(int id, IReadOnlyList<LectureContentChapterInputVm> chapters, string userId, CancellationToken ct = default);

        /// <summary>OLC-S3.2 — إضافة فيديو مؤشر (R5 + R6 لقطة + R7 + R8).</summary>
        Task<LectureContentResult> AddIndicatorVideoAsync(int id, AddIndicatorVideoVm vm, string userId, CancellationToken ct = default);

        /// <summary>حذف ناعم لعنصر. يُرفض حذف آخر عنصر في محتوى منشور (R5).</summary>
        Task<LectureContentResult> RemoveItemAsync(int itemId, string userId, CancellationToken ct = default);

        /// <summary>إعادة ترتيب قائمة التشغيل: يجب أن تطابق القائمة العناصر النشطة تمامًا.</summary>
        Task<LectureContentResult> ReorderItemsAsync(int id, IReadOnlyList<int> orderedItemIds, string userId, CancellationToken ct = default);

        /// <summary>R6 — «تحديث من المصدر» الصريح: يعيد أخذ لقطة الرابط من RemedialVideos.</summary>
        Task<LectureContentResult> RefreshItemFromSourceAsync(int itemId, string userId, CancellationToken ct = default);

        /// <summary>OLC-S3.3 — Draft/Hidden → Published بعد تحقق R4/R5 (+R1/R7/R8). يسجّل PublishedAt/By عند أول نشر.</summary>
        Task<LectureContentResult> PublishAsync(int id, string userId, CancellationToken ct = default);

        /// <summary>OLC-S3.3 — Published → Hidden. المحتوى والتقدّم يبقيان.</summary>
        Task<LectureContentResult> HideAsync(int id, string userId, CancellationToken ct = default);

        /// <summary>معرّف المحاضرة لعنصر (لإعادة التوجيه بعد عمليات العنصر).</summary>
        Task<int?> GetLectureIdOfItemAsync(int itemId, CancellationToken ct = default);

        // ═══════════════ OLC-S4.2 — دفعات الشركاء (R16/R18) ═══════════════

        /// <summary>شريك الدفعة (Branch.PartnerId). null = دفعة منصة أو غير موجودة.</summary>
        Task<int?> GetBatchPartnerIdAsync(int batchId, CancellationToken ct = default);

        /// <summary>شريك دفعة المحاضرة. null = دفعة منصة أو غير موجودة.</summary>
        Task<int?> GetLecturePartnerIdAsync(int lectureId, CancellationToken ct = default);

        /// <summary>شاشة «الدفعات الأونلاين»: «المنصة» أو «الشركاء» (عدة شركاء) مع حالة البند لكل شريك ودفعة.</summary>
        Task<OnlineBatchesPageVm> GetOnlineBatchesAsync(OnlineBatchesFilterVm filter, bool canManagePartners, CancellationToken ct = default);

        // ═══════════════ OLC-S7.3/S7.4 — المتابعة (بيانات حقيقية فقط) ═══════════════

        /// <summary>تقرير المشاهدة لمحتوى المحاضرة: المسجّلون/من بدأ/من أتم/المتوقف عند القفل + إحصاء كل فصل/فيديو + الطلاب. null = لا محتوى.</summary>
        Task<LectureContentReportVm?> GetReportAsync(int lectureId, CancellationToken ct = default);

        /// <summary>KPIs الدفعة الأونلاين: نسبة المنشور، متوسط زمن النشر بعد المحاضرة، نسبة الإتمام لكل منهج.</summary>
        Task<OnlineBatchKpisVm> GetBatchKpisAsync(int batchId, CancellationToken ct = default);
    }
}
