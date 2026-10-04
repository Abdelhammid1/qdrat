using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    public enum RemedialTrackExamStartStatus
    {
        Created,        // أُنشئت محاولة جديدة
        Existing,       // توجد محاولة لهذا الاختبار (استئناف أو نتيجة) — لا اختبار ثالث/مكرر
        NotFound,       // 404: التسجيل/المحور لا يخص الطالب
        Forbidden,      // أمر نشر ملغى/غير منشور بعد
        NeedsCode,      // حضوري: يلزم الرقم المرجعي
        Conflict,       // 409: المحور ليس في AwaitingExam101/102 (مثلًا الفيديوهات لم تكتمل)
        NoQuestions     // نموذج الاختبار بلا أسئلة صالحة
    }

    public sealed record RemedialTrackExamStartResult(RemedialTrackExamStartStatus Status, int AttemptId = 0, string? Message = null);

    public enum RemedialTrackSolveStatus
    {
        Ok,
        Closed,         // مُسلَّمة/منتهية ← حوّل لصفحة النتيجة
        NotFound,
        Forbidden
    }

    public sealed record RemedialTrackSolveResult(RemedialTrackSolveStatus Status, StudentRemedialTrackSolveVm? Model = null);

    public enum RemedialTrackSaveAnswerStatus
    {
        Ok,
        BadRequest,     // 400: الإجابة ليست ضمن خيارات السؤال / فارغة
        NotFound,       // 404: المحاولة/السؤال لا يخص الطالب
        Expired,        // 409: المحاولة مُسلَّمة أو انتهى وقتها
        Forbidden
    }

    public sealed record RemedialTrackSaveAnswerResult(RemedialTrackSaveAnswerStatus Status, int RemainingSeconds = 0, string? Message = null);

    public enum RemedialTrackSubmitStatus
    {
        Submitted,      // سُلِّمت الآن وصُحّحت
        AlreadyClosed,  // تسليم مزدوج/سبق إغلاقها ← نفس النتيجة
        NotFound,
        Forbidden
    }

    public sealed record RemedialTrackSubmitResult(RemedialTrackSubmitStatus Status);

    public enum RemedialTrackResultStatus
    {
        Ok,
        InProgress,     // لم تُسلَّم بعد ← حوّل لصفحة الحل
        NotFound,
        Forbidden
    }

    public sealed record RemedialTrackAttemptResult(RemedialTrackResultStatus Status, StudentRemedialTrackResultVm? Model = null);

    /// <summary>RTK-S5: اختبارا 101/102 — محرك خفيف مستقل (D2): بدء، حل، حفظ إجابة، تسليم، تصحيح، نتيجة.</summary>
    public interface IRemedialTrackExamService
    {
        /// <summary>RTK-S5.2: بوابة الوصول + الحالة AwaitingExam101/102 ثم إنشاء محاولة بأسئلة مجمّدة أو إرجاع الموجودة.</summary>
        Task<RemedialTrackExamStartResult> StartExamAsync(int studentId, int enrollmentId, int axisProgressId, CancellationToken ct = default);

        /// <summary>RTK-S5.2: نموذج صفحة الحل (بلا الإجابة الصحيحة — D15). المنتهي وقته يُصحَّح تلقائيًا ويُحوَّل للنتيجة.</summary>
        Task<RemedialTrackSolveResult> GetSolveAsync(int studentId, int attemptId, CancellationToken ct = default);

        /// <summary>RTK-S5.2: حفظ إجابة واحدة (مالك + InProgress + قبل الانتهاء + السؤال ضمن المحاولة + الإجابة ضمن الخيارات).</summary>
        Task<RemedialTrackSaveAnswerResult> SaveAnswerAsync(int studentId, int attemptId, Guid questionId, string? answer, CancellationToken ct = default);

        /// <summary>RTK-S5.3: تسليم + تصحيح + انتقال الحالة. مسموح حتى ExpiresAtUtc + 30ث. تسليم مزدوج ← نفس النتيجة.</summary>
        Task<RemedialTrackSubmitResult> SubmitAsync(int studentId, int attemptId, CancellationToken ct = default);

        /// <summary>RTK-S5.3: نتيجة محاولة مغلقة + المسار التالي (بلا كشف الإجابات).</summary>
        Task<RemedialTrackAttemptResult> GetResultAsync(int studentId, int attemptId, CancellationToken ct = default);
    }
}
