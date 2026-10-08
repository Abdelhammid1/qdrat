using QdratNew.Enums;

namespace QdratNew.ViewModels.RemedialTracks
{
    // ============ RTK-S11 (D23/D28): لقطة تقرير ولي الأمر + نماذج صفحة ولي الأمر ============

    /// <summary>
    /// لقطة JSON ثابتة (اسم الطالب والأرقام والتوصية فقط). لا معرّفات داخلية ولا بيانات اتصال ولا ملاحظات إدارة.
    /// </summary>
    public sealed class RemedialTrackParentReportSnapshot
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public string StudentName { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public string TrackTitle { get; set; } = string.Empty;
        public string CurriculumTitle { get; set; } = string.Empty;
        public DateTime IssuedAtUtc { get; set; }
        public int PassPercent { get; set; }
        public bool IsFinished { get; set; }

        /// <summary>المحور محل التقرير (لتقرير «عدم اجتياز محور» فقط).</summary>
        public int? FocusAxisOrder { get; set; }
        public string? FocusAxisTitle { get; set; }

        public int TotalAxes { get; set; }
        public int PassedAxes { get; set; }
        public int FollowUpAxes { get; set; }
        public double? AverageScorePercent { get; set; }

        /// <summary>RTK-S13/D33: الملاحق القائمة وقت إنشاء اللقطة فقط (لقطات سابقة = 0). لا تدخل في أي نسبة.</summary>
        public int AddendaTotal { get; set; }
        public int AddendaCompleted { get; set; }

        public List<RemedialTrackParentReportSnapshotAxis> Axes { get; set; } = new();
    }

    public sealed class RemedialTrackParentReportSnapshotAxis
    {
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public RemedialTrackReportAxisOutcome Outcome { get; set; }
        public RemedialTrackPathKind PathKind { get; set; }

        public int VideoTotal { get; set; }
        public int VideosDoneRound1 { get; set; }
        public int VideosDoneRound2 { get; set; }
        public bool HasRound2 { get; set; }
        public int WatchedMinutes { get; set; }
        public int VideoMinutes { get; set; }

        public double? Exam101Percent { get; set; }
        public double? Exam102Percent { get; set; }
        public DateTime? PassedAtUtc { get; set; }
        public DateTime? AdminOpenedAtUtc { get; set; }

        public string Recommendation { get; set; } = string.Empty;
        public List<RemedialTrackParentReportSnapshotAttempt> Attempts { get; set; } = new();
    }

    public sealed class RemedialTrackParentReportSnapshotAttempt
    {
        public RemedialTrackExamNumber ExamNumber { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public int CorrectCount { get; set; }
        public int TotalQuestions { get; set; }
        public double ScorePercent { get; set; }
        public bool IsPassed { get; set; }
        public int DurationMinutes { get; set; }
    }

    // ---- صفحة ولي الأمر ----

    public sealed class RemedialTrackParentReportListItemVm
    {
        public int Id { get; set; }
        public RemedialTrackParentReportKind Kind { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string TrackTitle { get; set; } = string.Empty;
        public string? FocusAxisTitle { get; set; }
        public DateTime SentAtUtc { get; set; }
        public DateTime? AcknowledgedAtUtc { get; set; }
    }

    public sealed class RemedialTrackParentReportListVm
    {
        public const int PageSize = 15;

        public IReadOnlyList<RemedialTrackParentReportListItemVm> Items { get; set; } = Array.Empty<RemedialTrackParentReportListItemVm>();
        public int Page { get; set; } = 1;
        public int Total { get; set; }
        public int TotalPages => Total <= 0 ? 1 : (int)Math.Ceiling(Total / (double)PageSize);
    }

    public sealed class RemedialTrackParentReportDetailsVm
    {
        public int Id { get; set; }
        public RemedialTrackParentReportKind Kind { get; set; }
        public DateTime SentAtUtc { get; set; }
        public DateTime? AcknowledgedAtUtc { get; set; }
        public RemedialTrackParentReportSnapshot Snapshot { get; set; } = new();
    }

    /// <summary>نتيجة إجراء ولي الأمر (إقرار): Success=false ← غير موجود/غير مملوك (يُترجم إلى 404).</summary>
    public sealed record RemedialTrackParentAckResult(bool Found, bool AlreadyAcknowledged, DateTime? AcknowledgedAtUtc);
}

namespace QdratNew.ViewModels.RemedialTracks
{
    // ============ RTK-S12.1: طابور تقارير أولياء الأمور للأدمن ============

    public sealed class RemedialTrackParentReportQueueFilter
    {
        public RemedialTrackParentReportStatus? Status { get; set; }
        public int? PublicationId { get; set; }
        public int? BatchId { get; set; }
        public int Page { get; set; } = 1;
    }

    public sealed class RemedialTrackParentReportQueueItemVm
    {
        public int Id { get; set; }
        public int EnrollmentId { get; set; }
        public int PublicationId { get; set; }
        public RemedialTrackParentReportKind Kind { get; set; }
        public RemedialTrackParentReportStatus Status { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string? ParentName { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public string? FocusAxisTitle { get; set; }
        public bool HasSnapshot { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? SentAtUtc { get; set; }
        public DateTime? AcknowledgedAtUtc { get; set; }
    }

    public sealed class RemedialTrackParentReportQueueLookup
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class RemedialTrackParentReportQueueVm
    {
        public const int PageSize = 20;

        public IReadOnlyList<RemedialTrackParentReportQueueItemVm> Items { get; set; } = Array.Empty<RemedialTrackParentReportQueueItemVm>();
        public RemedialTrackParentReportQueueFilter Filter { get; set; } = new();
        public int Page { get; set; } = 1;
        public int Total { get; set; }
        public int TotalPages => Total <= 0 ? 1 : (int)Math.Ceiling(Total / (double)PageSize);

        /// <summary>عدد التقارير لكل حالة ضمن الأمر/الدفعة المختارة (بمعزل عن فلتر الحالة).</summary>
        public IReadOnlyDictionary<RemedialTrackParentReportStatus, int> StatusCounts { get; set; }
            = new Dictionary<RemedialTrackParentReportStatus, int>();

        public IReadOnlyList<RemedialTrackParentReportQueueLookup> Publications { get; set; } = Array.Empty<RemedialTrackParentReportQueueLookup>();
        public IReadOnlyList<RemedialTrackParentReportQueueLookup> Batches { get; set; } = Array.Empty<RemedialTrackParentReportQueueLookup>();

        public bool CanSend { get; set; }
        public bool CanToggleAutoSend { get; set; }
    }

    /// <summary>نتيجة إجراء أدمن على تقرير ولي الأمر: Found=false ← غير موجود/خارج نطاق الدفعات (يُترجم إلى 404).</summary>
    public sealed record RemedialTrackParentReportActionResult(bool Found, bool Success, string Message)
    {
        public static RemedialTrackParentReportActionResult NotFound { get; } = new(false, false, "غير موجود.");
        public static RemedialTrackParentReportActionResult Ok(string message) => new(true, true, message);
        public static RemedialTrackParentReportActionResult Fail(string message) => new(true, false, message);
    }

    /// <summary>نموذج POST (لا Overposting: المعرّف فقط).</summary>
    public sealed class RemedialTrackParentReportActionInput
    {
        public int Id { get; set; }
    }

    public sealed class ToggleRemedialTrackAutoSendInput
    {
        public int PublicationId { get; set; }
        public bool Enabled { get; set; }
    }
}
