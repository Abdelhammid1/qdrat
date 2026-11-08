using QdratNew.Enums;

namespace QdratNew.ViewModels.OnlineLecture
{
    // ═════════════════════════════ OLC-S7.3 — تقرير المشاهدة لمحاضرة ═════════════════════════════
    // بيانات حقيقية فقط: من جداول التقدّم (LectureContentProgress / LectureContentItemProgress) والتسجيلات النشطة.

    /// <summary>حالة الطالب في محاضرة واحدة (ترتيب الأولوية: أتم ← مقفلة ← بدأ ← لم يبدأ).</summary>
    public enum LectureReportStudentState
    {
        NotStarted = 0,
        InProgress = 1,
        Completed = 2,
        /// <summary>R13: لم يُتمّ المحاضرات السابقة في نفس المنهج — متوقف عند القفل قبل هذه المحاضرة.</summary>
        Locked = 3
    }

    public class LectureContentReportVm
    {
        public int LectureId { get; set; }
        public int ContentId { get; set; }
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string LectureTitle { get; set; } = string.Empty;
        public DateTime LectureDate { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public string CurriculumTitle { get; set; } = string.Empty;
        public string ContentTitle { get; set; } = string.Empty;
        public LectureContentMode Mode { get; set; }
        public LectureContentStatus Status { get; set; }
        public DateTime? PublishedAt { get; set; }
        public int? DurationSeconds { get; set; }

        /// <summary>ترتيب المحاضرة في تسلسل المنهج (R13) — 0 إن لم تكن منشورة.</summary>
        public int SequencePosition { get; set; }
        public int SequenceCount { get; set; }

        public int EnrolledCount { get; set; }
        public int StartedCount { get; set; }
        public int CompletedCount { get; set; }
        public int LockedCount { get; set; }
        public int NotStartedCount { get; set; }
        public int InProgressCount { get; set; }

        public List<LectureContentReportItemVm> Items { get; set; } = new();
        public List<LectureContentReportStudentVm> Students { get; set; } = new();

        public bool FeatureEnabled { get; set; }
        public bool IsPublished => Status == LectureContentStatus.Published;

        public static int Percent(int part, int total) => total <= 0 ? 0 : (int)Math.Round(part * 100.0 / total);
    }

    /// <summary>
    /// فصل (FullVideo) أو فيديو مؤشر (IndicatorVideos). ReachedEnd: FullVideo = MaxPositionSeconds ≥ EndSeconds − 3 (R9)،
    /// IndicatorVideos = شاهد النهاية (EndedSeen). StoppedHere = بدأ المحاضرة ولم يُتمّها وهذا أول جزء لم يصل لنهايته (القفل R14/P4).
    /// </summary>
    public class LectureContentReportItemVm
    {
        public int Id { get; set; }
        public int Position { get; set; }
        public string Title { get; set; } = string.Empty;
        public string LessonTitle { get; set; } = string.Empty;
        public int? StartSeconds { get; set; }
        public int? EndSeconds { get; set; }
        public int? DurationSeconds { get; set; }

        public int StartedCount { get; set; }
        public int ReachedEndCount { get; set; }
        /// <summary>IndicatorVideos فقط: أتمّ الفيديو (ended + 80%).</summary>
        public int CompletedCount { get; set; }
        public int StoppedHereCount { get; set; }
    }

    public class LectureContentReportStudentVm
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public LectureReportStudentState State { get; set; }
        public int ProgressPercent { get; set; }
        /// <summary>IndicatorVideos: عدد الفيديوهات المكتملة. FullVideo: عدد الفصول التي وصل لنهايتها.</summary>
        public int CompletedParts { get; set; }
        public int WatchedSeconds { get; set; }
        public DateTime? FirstActivityUtc { get; set; }
        public DateTime? LastActivityUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        /// <summary>عنوان الجزء الذي توقف عنده (بدأ ولم يُتم).</summary>
        public string? StoppedAt { get; set; }
    }

    public static class LectureReportStudentStateText
    {
        public static string Label(LectureReportStudentState s) => s switch
        {
            LectureReportStudentState.Completed => "أتمّ",
            LectureReportStudentState.InProgress => "بدأ",
            LectureReportStudentState.Locked => "متوقف عند القفل",
            _ => "لم يبدأ"
        };

        public static string Css(LectureReportStudentState s) => s switch
        {
            LectureReportStudentState.Completed => "olc-badge--published",
            LectureReportStudentState.InProgress => "olc-badge--online",
            LectureReportStudentState.Locked => "olc-badge--draft",
            _ => "olc-badge--none"
        };
    }

    // ═════════════════════════════ OLC-S7.4 — KPIs الدفعة الأونلاين ═════════════════════════════

    public class OnlineBatchKpisVm
    {
        public int LecturesCount { get; set; }
        public int PublishedCount { get; set; }
        public int PublishedPercent { get; set; }

        /// <summary>
        /// متوسط (أول نشر − موعد المحاضرة) بالساعات للمحتوى المنشور الذي له PublishedAt. null = لا بيانات.
        /// قيمة سالبة = نُشر قبل موعد المحاضرة.
        /// </summary>
        public double? AveragePublishDelayHours { get; set; }
        public int PublishDelaySamples { get; set; }

        public int EnrolledCount { get; set; }
        public List<OnlineBatchCurriculumKpiVm> Curricula { get; set; } = new();

        public bool HasData => LecturesCount > 0;
    }

    public class OnlineBatchCurriculumKpiVm
    {
        public int CurriculumId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int PublishedLectures { get; set; }
        /// <summary>إتمامات الطلاب المسجّلين ÷ (المسجّلون × المحاضرات المنشورة).</summary>
        public int CompletionPercent { get; set; }
        /// <summary>طلاب أتمّوا كل المحاضرات المنشورة في المنهج.</summary>
        public int StudentsCompletedAll { get; set; }
    }
}
