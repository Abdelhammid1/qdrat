using QdratNew.Enums;

namespace QdratNew.ViewModels.RemedialTracks
{
    // ============ RTK-S6: تقرير الطالب / تقرير ولي الأمر (نفس البيانات، بلا أي رقم مرجعي ولا بيانات طلاب آخرين) ============

    /// <summary>الحالة النهائية لمحور داخل التقرير.</summary>
    public enum RemedialTrackReportAxisOutcome
    {
        Passed = 1,          // اجتاز
        NeedsFollowUp = 2,   // يحتاج متابعة (FailedBlocked)
        MovedByAdmin = 3,    // نُقل بقرار الإدارة دون اجتياز (FailedOpenedByAdmin)
        InProgress = 4,      // قيد التنفيذ
        NotReached = 5       // لم يصل بعد
    }

    public sealed class RemedialTrackReportAxisVm
    {
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public RemedialTrackAxisStatus Status { get; set; }
        public RemedialTrackReportAxisOutcome Outcome { get; set; }
        public int Round { get; set; }

        public int VideoTotal { get; set; }
        public int VideosDoneRound1 { get; set; }
        public int VideosDoneRound2 { get; set; }
        public bool HasRound2 { get; set; }

        public double? Exam101Percent { get; set; }
        public DateTime? Exam101SubmittedAtUtc { get; set; }
        public double? Exam102Percent { get; set; }
        public DateTime? Exam102SubmittedAtUtc { get; set; }

        public DateTime? PassedAtUtc { get; set; }
        /// <summary>تاريخ فتح الإدارة للمحور التالي (إن حدث). لا يُعرض السبب ولا اسم الأدمن في تقرير الطالب/ولي الأمر.</summary>
        public DateTime? AdminOpenedAtUtc { get; set; }

        /// <summary>أفضل/آخر نسبة اختبار للمحور (102 إن وُجد وإلا 101).</summary>
        public double? FinalPercent => Exam102Percent ?? Exam101Percent;
    }

    public sealed class RemedialTrackEnrollmentReportVm
    {
        public int EnrollmentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public string TrackTitle { get; set; } = string.Empty;
        public string CurriculumTitle { get; set; } = string.Empty;
        public RemedialTrackDeliveryMode Mode { get; set; }
        public DateTime PublishAtUtc { get; set; }
        public RemedialTrackEnrollmentStatus Status { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public int PassPercent { get; set; }
        public DateTime IssuedAtUtc { get; set; }

        /// <summary>ملاحظة الأدمن — تُملأ في تقرير ولي الأمر فقط (لا تصل لتقرير الطالب).</summary>
        public string? AdminNote { get; set; }

        /// <summary>يُحدَّد في الكنترولر: هل يملك المستخدم RemedialTrackReports:Edit (يظهر محرّر الملاحظة).</summary>
        public bool CanEditNote { get; set; }

        public IReadOnlyList<RemedialTrackReportAxisVm> Axes { get; set; } = Array.Empty<RemedialTrackReportAxisVm>();

        public int TotalAxes => Axes.Count;
        public int PassedAxes => Axes.Count(a => a.Outcome == RemedialTrackReportAxisOutcome.Passed);
        public int FollowUpAxes => Axes.Count(a => a.Outcome is RemedialTrackReportAxisOutcome.NeedsFollowUp or RemedialTrackReportAxisOutcome.MovedByAdmin);
        public int MovedByAdminAxes => Axes.Count(a => a.Outcome == RemedialTrackReportAxisOutcome.MovedByAdmin);
        public int PassedPercent => TotalAxes == 0 ? 0 : (int)Math.Round(PassedAxes * 100.0 / TotalAxes);
        public bool IsFinished => Status is RemedialTrackEnrollmentStatus.Completed or RemedialTrackEnrollmentStatus.CompletedWithFailures;

        /// <summary>متوسط النسب النهائية للمحاور التي أُجري لها اختبار (null إن لم يوجد).</summary>
        public double? AverageScorePercent
        {
            get
            {
                var scores = Axes.Where(a => a.FinalPercent.HasValue).Select(a => a.FinalPercent!.Value).ToList();
                return scores.Count == 0 ? null : Math.Round(scores.Average(), 1);
            }
        }
    }

    // ============ RTK-S6: تقرير الدفعة (جدول ملخص قابل للطباعة) ============

    public sealed class RemedialTrackBatchReportRowVm
    {
        public int EnrollmentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public RemedialTrackEnrollmentStatus Status { get; set; }
        public int PassedAxes { get; set; }
        public int FollowUpAxes { get; set; }
        public DateTime? LastActivityUtc { get; set; }
    }

    public sealed class RemedialTrackBatchReportVm
    {
        public int PublicationId { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public string CurriculumTitle { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public RemedialTrackDeliveryMode Mode { get; set; }
        public DateTime PublishAtUtc { get; set; }
        public int TotalAxes { get; set; }
        public DateTime IssuedAtUtc { get; set; }
        public IReadOnlyList<RemedialTrackBatchReportRowVm> Rows { get; set; } = Array.Empty<RemedialTrackBatchReportRowVm>();
    }

    // ============ RTK-S6: لوحة متابعة أمر النشر (الأدمن) ============

    public sealed class RemedialTrackDashboardFilter
    {
        public const int PageSize = 25;
        public const int MaxSearchLength = 100;

        public RemedialTrackEnrollmentStatus? Status { get; set; }
        public bool BlockedOnly { get; set; }
        public string? Q { get; set; }
        public int Page { get; set; } = 1;
    }

    public sealed class RemedialTrackDashboardKpisVm
    {
        public int Total { get; set; }
        public int NotStarted { get; set; }
        public int InProgress { get; set; }
        public int Completed { get; set; }
        public int CompletedWithFailures { get; set; }
        public int Cancelled { get; set; }
        /// <summary>طلاب لديهم محور في حالة FailedBlocked وبلا فتح، ولهم محور تالٍ يمكن فتحه.</summary>
        public int BlockedAwaitingAdmin { get; set; }
    }

    /// <summary>صف الرسم الثاني: نتائج كل محور (تجميع GROUP BY في الخادم، لا حساب في الـ View).</summary>
    public sealed class RemedialTrackAxisChartRowVm
    {
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Attempted { get; set; }            // من أدّوا اختبار 101
        public int PassedFirstTime { get; set; }      // اجتاز في 101
        public int PassedAfterRewatch { get; set; }   // اجتاز في 102 بعد الإعادة
        public int NotPassed { get; set; }            // لم يجتز المحور (محجوب أو نُقل بقرار الإدارة)
    }

    public sealed class RemedialTrackDashboardStudentRowVm
    {
        public int EnrollmentId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public RemedialTrackEnrollmentStatus Status { get; set; }

        public string? CurrentAxisTitle { get; set; }
        public int? CurrentAxisOrder { get; set; }
        public int? CurrentRound { get; set; }

        public RemedialTrackExamNumber? LastExamNumber { get; set; }
        public double? LastScorePercent { get; set; }
        public DateTime? LastActivityUtc { get; set; }

        /// <summary>يُملأ فقط للمحجوبين الذين لهم محور تالٍ — يفعّل زر «فتح المحور التالي».</summary>
        public int? BlockedAxisProgressId { get; set; }
        public string? BlockedAxisTitle { get; set; }
        public bool IsBlocked => BlockedAxisProgressId.HasValue;
    }

    public sealed class RemedialTrackNotPassedRowVm
    {
        public int EnrollmentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string AxisTitle { get; set; } = string.Empty;
        public RemedialTrackAxisStatus Status { get; set; }

        // تفاصيل الجلسة (للجدول المخصّص للطباعة والمتابعة)
        public int AxisProgressId { get; set; }
        public int AxisOrder { get; set; }
        public int Round { get; set; }
        public double? Exam101Percent { get; set; }
        public double? Exam102Percent { get; set; }
        public int AttemptsCount { get; set; }
        public DateTime? FailedAtUtc { get; set; }
        public string? AdminOpenedByName { get; set; }
        public string? AdminOpenReason { get; set; }
        public DateTime? AdminOpenedAtUtc { get; set; }
        /// <summary>محجوب ويمكن للإدارة فتح المحور التالي له (FailedBlocked قبل آخر محور).</summary>
        public bool CanUnlockNext { get; set; }
    }

    public sealed class RemedialTrackDashboardVm
    {
        public const int MaxNotPassedListed = 300;

        public RemedialTrackDashboardKpisVm Kpis { get; set; } = new();
        public IReadOnlyList<RemedialTrackAxisChartRowVm> AxisChart { get; set; } = Array.Empty<RemedialTrackAxisChartRowVm>();
        public IReadOnlyList<RemedialTrackDashboardStudentRowVm> Students { get; set; } = Array.Empty<RemedialTrackDashboardStudentRowVm>();
        public IReadOnlyList<RemedialTrackNotPassedRowVm> NotPassed { get; set; } = Array.Empty<RemedialTrackNotPassedRowVm>();
        public bool NotPassedTruncated { get; set; }

        public RemedialTrackDashboardFilter Filter { get; set; } = new();
        public int Page { get; set; } = 1;
        public int Total { get; set; }
        public int TotalPages => Total <= 0 ? 1 : (int)Math.Ceiling(Total / (double)RemedialTrackDashboardFilter.PageSize);

        // صلاحيات العرض (تُحدَّد في الكنترولر)
        public bool CanUnlock { get; set; }
        public bool CanReadReports { get; set; }
    }

    // ============ RTK-S6: مدخلات POST (ViewModels مخصّصة — لا Overposting) ============

    public sealed class UnlockRemedialTrackAxisInput
    {
        public int EnrollmentId { get; set; }
        public int AxisProgressId { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class SaveRemedialTrackReportNoteInput
    {
        public int EnrollmentId { get; set; }
        public string? Note { get; set; }
    }
}
