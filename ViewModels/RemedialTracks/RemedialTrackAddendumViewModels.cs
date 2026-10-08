using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.ViewModels.RemedialTracks
{
    // ============ RTK-S13 (D29–D34): ملحق المحور ============

    // ───────────── الأدمن ─────────────

    /// <summary>جسم نموذج إنشاء ملحق (POST). نموذج مخصّص لمنع الـ overposting؛ يُحوَّل إلى <see cref="CreateAddendumInput"/> في الكنترولر.</summary>
    public sealed class CreateRemedialTrackAddendumInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "أمر النشر غير صالح.")]
        public int PublicationId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "اختر المحور.")]
        public int AxisId { get; set; }

        [Required(ErrorMessage = "عنوان الفيديو مطلوب."), StringLength(200)]
        public string? Title { get; set; }

        [Required(ErrorMessage = "رابط الفيديو مطلوب."), StringLength(500)]
        public string? Url { get; set; }

        public int? DurationSeconds { get; set; }

        [Required(ErrorMessage = "سبب الإضافة مطلوب (5–300 حرف)."), StringLength(300, MinimumLength = 5, ErrorMessage = "سبب الإضافة بين 5 و300 حرف.")]
        public string? Reason { get; set; }

        public int? ExamModelId { get; set; }
        public int? ExamDurationMinutes { get; set; }

        public bool ApplyToAll { get; set; }
        public List<int>? EnrollmentIds { get; set; }
    }

    public sealed class SetRemedialTrackAddendumActiveInput
    {
        [Range(1, int.MaxValue)] public int AddendumId { get; set; }
        public bool Active { get; set; }
    }

    /// <summary>مدخل الخدمة. ApplyToAll = «كل تسجيلات أمر النشر»، وإلا قائمة معرّفات تسجيل (≤ 500).</summary>
    public sealed record CreateAddendumInput(
        int PublicationId,
        int AxisId,
        string? Title,
        string? Url,
        int? DurationSeconds,
        string? Reason,
        int? ExamModelId,
        int? ExamDurationMinutes,
        bool ApplyToAll,
        IReadOnlyList<int>? EnrollmentIds);

    /// <summary>صف متابعة ملحق واحد (مجمَّع بلا حلقات استعلام).</summary>
    public sealed class AddendumTrackingRow
    {
        public int AddendumId { get; set; }
        public int AxisOrder { get; set; }
        public string AxisTitle { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool HasExam { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public int Targeted { get; set; }
        public int VideoCompleted { get; set; }
        public int ExamPassed { get; set; }
        public int Completed { get; set; }
        public int AttemptsTotal { get; set; }
        public DateTime? LastActivityUtc { get; set; }
    }

    /// <summary>صف طالب داخل ملحق (للأدمن، مرقّم من الخادم).</summary>
    public sealed class AddendumStudentRow
    {
        public int EnrollmentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public int WatchedPercent { get; set; }
        public bool VideoCompleted { get; set; }
        public bool ExamPassed { get; set; }
        public double? BestScorePercent { get; set; }
        public int AttemptsCount { get; set; }
        public DateTime? LastActivityUtc { get; set; }
        public bool Completed { get; set; }
    }

    public sealed class AddendumStudentsPage
    {
        public const int PageSize = 20;
        public IReadOnlyList<AddendumStudentRow> Items { get; set; } = Array.Empty<AddendumStudentRow>();
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int Total { get; set; }
    }

    /// <summary>لوحة «الملاحق» في صفحة تفاصيل أمر النشر.</summary>
    public sealed class RemedialTrackAddendaPanelVm
    {
        public const int PageSize = 10;

        public bool CanManage { get; set; }
        public bool PublicationActive { get; set; }
        public IReadOnlyList<RemedialTrackSelectOption> Axes { get; set; } = Array.Empty<RemedialTrackSelectOption>();
        public IReadOnlyList<RemedialTrackSelectOption> ExamModels { get; set; } = Array.Empty<RemedialTrackSelectOption>();
        public IReadOnlyList<AddendumTrackingRow> Rows { get; set; } = Array.Empty<AddendumTrackingRow>();
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int Total { get; set; }
    }

    // ───────────── الطالب ─────────────

    public enum StudentAddendumState
    {
        NotStarted = 0,   // لم يبدأ
        Watching = 1,     // بدأ المشاهدة
        Watched = 2,      // شاهد (الاختبار لم يبدأ)
        RetakeExam = 3,   // يحتاج إعادة الاختبار
        Completed = 4     // اجتاز / اكتمل
    }

    /// <summary>بطاقة «مطلوب إضافي» أسفل المحور المعني في صفحة الخطة.</summary>
    public sealed class StudentRemedialTrackAddendumCardVm
    {
        public int AddendumId { get; set; }
        public int AxisId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public bool HasExam { get; set; }
        public StudentAddendumState State { get; set; }
    }

    public sealed class StudentAddendumAttemptVm
    {
        public int AttemptId { get; set; }
        public double ScorePercent { get; set; }
        public bool IsPassed { get; set; }
        public DateTime SubmittedAtLocal { get; set; }
    }

    /// <summary>صفحة الملحق للطالب. الرابط/المعرّف لا يُسلَّم إلا أثناء المشاهدة المطلوبة (قبل اكتمال الفيديو).</summary>
    public sealed class StudentAddendumVm
    {
        public int EnrollmentId { get; set; }
        public int AddendumId { get; set; }
        public int AddendumProgressId { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public string AxisTitle { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;

        public RemedialTrackVideoProvider Provider { get; set; }
        public string? ExternalId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? EmbedUrl { get; set; }
        public int? DurationSeconds { get; set; }
        public int WatchedSeconds { get; set; }
        public int RequiredSeconds { get; set; }
        public bool VideoCompleted { get; set; }

        public bool HasExam { get; set; }
        public int ExamDurationMinutes { get; set; }
        public bool ExamPassed { get; set; }
        public double? BestScorePercent { get; set; }
        public int AttemptsCount { get; set; }
        public int? InProgressAttemptId { get; set; }
        public IReadOnlyList<StudentAddendumAttemptVm> Attempts { get; set; } = Array.Empty<StudentAddendumAttemptVm>();

        public bool IsCompleted { get; set; }
        public StudentAddendumState State { get; set; }

        public string WatermarkText { get; set; } = string.Empty;
        public string WatermarkIdText { get; set; } = string.Empty;
    }

    /// <summary>جسم نبضة فيديو الملحق (JSON). الرد نفس <see cref="RemedialTrackVideoPingResponse"/>.</summary>
    public sealed class RemedialTrackAddendumPingRequest
    {
        public int EnrollmentId { get; set; }
        public int AddendumProgressId { get; set; }
        public string? State { get; set; }        // playing|paused|ended|manual-done
        public double Position { get; set; }
        public double Duration { get; set; }
    }

    /// <summary>تلخيص الملاحق في تقرير الطالب/الأدمن (سطر «مطلوب إضافي»، D33) — لا يدخل في نسبة المحور ولا حالة ولي الأمر.</summary>
    public sealed class RemedialTrackReportAddendumVm
    {
        public string Title { get; set; } = string.Empty;
        public string AxisTitle { get; set; } = string.Empty;
        public bool Completed { get; set; }
    }
}
