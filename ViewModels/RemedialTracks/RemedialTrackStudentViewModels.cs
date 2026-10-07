using System.Text.Json.Serialization;
using QdratNew.Enums;

namespace QdratNew.ViewModels.RemedialTracks
{
    /// <summary>RTK-S4.1: صف في صفحة «خطتي العلاجية» (أمر نشر واحد للطالب).</summary>
    public sealed class StudentRemedialTrackListItemVm
    {
        public int EnrollmentId { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public string CurriculumName { get; set; } = string.Empty;
        public DateTime TrackCreatedAtLocal { get; set; }
        public DateTime PublishAtLocal { get; set; }
        public RemedialTrackDeliveryMode Mode { get; set; }
        public RemedialTrackEnrollmentStatus Status { get; set; }
        public int TotalAxes { get; set; }
        public int PassedAxes { get; set; }
        public int ProgressPercent => TotalAxes == 0 ? 0 : (int)Math.Round(PassedAxes * 100.0 / TotalAxes);
    }

    public sealed class StudentRemedialTrackIndexVm
    {
        public IReadOnlyList<StudentRemedialTrackListItemVm> Items { get; set; } = Array.Empty<StudentRemedialTrackListItemVm>();
    }

    /// <summary>RTK-S4.2: نموذج بوابة الرقم المرجعي.</summary>
    public sealed class StudentRemedialTrackCodeGateVm
    {
        public int EnrollmentId { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public DateTime? LockedUntilLocal { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>نموذج صفحة إقرار شروط الخطة.</summary>
    public sealed class StudentRemedialTrackTermsVm
    {
        public int EnrollmentId { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    public sealed class StudentRemedialTrackAcceptTermsInput
    {
        public int EnrollmentId { get; set; }
        public bool Accepted { get; set; }
    }

    public sealed class StudentRemedialTrackVerifyCodeInput
    {
        public int EnrollmentId { get; set; }
        public string? Code { get; set; }
    }

    /// <summary>RTK-S4.3: محور في صفحة الخطة.</summary>
    public sealed class StudentRemedialTrackAxisItemVm
    {
        public int AxisProgressId { get; set; }
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public RemedialTrackAxisStatus Status { get; set; }
        public int Round { get; set; }
        public int VideoCount { get; set; }
        public double? Exam101Percent { get; set; }
        public double? Exam102Percent { get; set; }

        public bool IsLocked => Status == RemedialTrackAxisStatus.Locked;
        public bool IsCurrent => Status is RemedialTrackAxisStatus.Videos or RemedialTrackAxisStatus.AwaitingExam101
            or RemedialTrackAxisStatus.Rewatch or RemedialTrackAxisStatus.AwaitingExam102;
        public bool IsPassed => Status == RemedialTrackAxisStatus.Passed;
        public bool IsNotPassed => Status is RemedialTrackAxisStatus.FailedBlocked or RemedialTrackAxisStatus.FailedOpenedByAdmin;
    }

    public sealed class StudentRemedialTrackPlanVm
    {
        public int EnrollmentId { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public string? TrackDescription { get; set; }
        public string CurriculumName { get; set; } = string.Empty;
        public RemedialTrackDeliveryMode Mode { get; set; }
        public RemedialTrackEnrollmentStatus Status { get; set; }
        public IReadOnlyList<StudentRemedialTrackAxisItemVm> Axes { get; set; } = Array.Empty<StudentRemedialTrackAxisItemVm>();
    }

    /// <summary>RTK-S4.3/4.4: فيديو داخل صفحة المحور.</summary>
    public sealed class StudentRemedialTrackVideoItemVm
    {
        public int VideoProgressId { get; set; }
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public RemedialTrackVideoProvider Provider { get; set; }
        public string? ExternalId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? EmbedUrl { get; set; }     // YouTube/Vimeo فقط — يُبنى من ExternalId لا من النص المُدخل
        public int? DurationSeconds { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsUnlocked { get; set; }      // السابق مكتمل (تسلسلي)
        public int WatchedSeconds { get; set; }
        public int RequiredSeconds { get; set; }
    }

    public sealed class StudentRemedialTrackAxisVm
    {
        public int EnrollmentId { get; set; }
        public int AxisProgressId { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public string AxisTitle { get; set; } = string.Empty;
        public int AxisOrder { get; set; }
        public RemedialTrackAxisStatus Status { get; set; }
        public int Round { get; set; }
        public bool CanWatch { get; set; }        // Videos/Rewatch فقط
        public bool VideosClosed { get; set; }    // انتهى المحور (اجتاز/انتقل للتالي): لا تُعرض الفيديوهات ولا روابطها
        public bool IsReviewMode { get; set; }    // RTK v2/D24: وضع مراجعة (للقراءة فقط): الروابط مسلَّمة بلا نبضات ولا تقدّم
        public DateTime? ReviewUntilLocal { get; set; }   // بتوقيت السعودية؛ null = مفتوحة حتى يغلقها الأدمن
        public bool AllVideosDone { get; set; }
        public bool ExamReady { get; set; }       // AwaitingExam101/102
        public RemedialTrackExamNumber? PendingExam { get; set; }
        public string WatermarkText { get; set; } = string.Empty;          // علامة مائية فوق الفيديو: اسم الطالب (السطر الأول)
        public string WatermarkIdText { get; set; } = string.Empty;        // السطر الثاني: رقم الهوية المسجل في حساب الطالب
        public int ExamDurationMinutes { get; set; }                       // RTK-S5
        public int? InProgressAttemptId { get; set; }                      // RTK-S5: محاولة الاختبار المعلّق قيد الحل
        public IReadOnlyList<StudentRemedialTrackAttemptItemVm> Attempts { get; set; } = Array.Empty<StudentRemedialTrackAttemptItemVm>();
        public IReadOnlyList<StudentRemedialTrackVideoItemVm> Videos { get; set; } = Array.Empty<StudentRemedialTrackVideoItemVm>();
    }

    /// <summary>RTK-S4.4: جسم نبضة الفيديو (JSON).</summary>
    public sealed class RemedialTrackVideoPingRequest
    {
        public int EnrollmentId { get; set; }
        public int VideoProgressId { get; set; }
        public string? State { get; set; }        // playing|paused|ended|manual-done
        public double Position { get; set; }
        public double Duration { get; set; }
    }

    public sealed class RemedialTrackNextVideoDto
    {
        [JsonPropertyName("videoProgressId")] public int VideoProgressId { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("provider")] public string Provider { get; set; } = string.Empty;
        [JsonPropertyName("externalId")] public string? ExternalId { get; set; }
        [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
        [JsonPropertyName("embedUrl")] public string? EmbedUrl { get; set; }     // يُبنى من ExternalId في الخادم — الصفحة لا تعتمد على بيانات مخفية في HTML
    }

    public sealed class RemedialTrackVideoPingResponse
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("reason")] public string? Reason { get; set; }       // insufficient | locked | state | ...
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("completed")] public bool Completed { get; set; }
        [JsonPropertyName("watchedSeconds")] public int WatchedSeconds { get; set; }
        [JsonPropertyName("requiredSeconds")] public int RequiredSeconds { get; set; }
        [JsonPropertyName("nextVideo")] public RemedialTrackNextVideoDto? NextVideo { get; set; }
        [JsonPropertyName("allDone")] public bool AllDone { get; set; }
        [JsonPropertyName("needsCode")] public bool NeedsCode { get; set; }
    }
}
