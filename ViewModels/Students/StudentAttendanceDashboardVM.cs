using System.Globalization;

namespace QdratNew.ViewModels.Students
{
    public class StudentAttendanceDashboardVM
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = "";
        public string BatchNames { get; set; } = "";

        // ── إجماليات ─────────────────────────────────
        public int TotalLectures { get; set; }
        public int AttendedCount { get; set; }
        public int AbsentCount { get; set; }
        public int ExcusedCount { get; set; }
        public int LateCount { get; set; }

        public double AttendanceRate =>
            TotalLectures == 0 ? 0 : Math.Round((double)AttendedCount / TotalLectures * 100, 1);

        public string AttendanceRateColor => AttendanceRate switch
        {
            >= 85 => "success",
            >= 70 => "warning",
            _ => "danger"
        };

        public string AttendanceRateLabel => AttendanceRate switch
        {
            >= 85 => "ممتاز",
            >= 70 => "مقبول",
            >= 50 => "ضعيف",
            _ => "حرج"
        };

        // ── سلسلة الحضور ─────────────────────────────
        public int CurrentStreak { get; set; }
        public int BestStreak { get; set; }

        // ── سجلات الحضور ─────────────────────────────
        public List<AttendanceRecordItemVM> Records { get; set; } = new();

        // ── ملخص حسب الدورة ──────────────────────────
        public List<CourseAttendanceSummaryVM> CourseSummaries { get; set; } = new();

        // ── ملخص شهري ────────────────────────────────
        public List<MonthlyAttendanceVM> MonthlyStats { get; set; } = new();

        // ── دُفعات الطالب ─────────────────────────────
        public List<BatchMiniVM> Batches { get; set; } = new();

        // ── توصيات الذكاء الاصطناعي ──────────────────
        public List<AIAttendanceRecommendationVM> AIRecommendations { get; set; } = new();

        // ── فلاتر الصفحة ─────────────────────────────
        public int? FilterBatchId { get; set; }
        public int? FilterCourseId { get; set; }
        public string? FilterMonth { get; set; }
    }

    public class AttendanceRecordItemVM
    {
        public int LectureId { get; set; }
        public string LectureTitle { get; set; } = "";
        public DateTime Date { get; set; }
        public string? Course { get; set; }
        public int? CourseId { get; set; }
        public string? Section { get; set; }
        public string? BatchName { get; set; }
        public int? BatchId { get; set; }
        public bool IsPresent { get; set; }
        public bool IsLateArrival { get; set; }
        public bool HasEarlyLeavePermission { get; set; }
        public string? Notes { get; set; }
        public TimeSpan? ActualArrivalTime { get; set; }
        public TimeSpan? ScheduledTime { get; set; }
        public string? InstructorName { get; set; }

        public bool IsExcused => !IsPresent && !string.IsNullOrWhiteSpace(Notes);

        public string StatusText => IsPresent
            ? (IsLateArrival ? "حضر متأخراً" : "حضر")
            : (IsExcused ? "غائب بعذر" : "غائب");

        public string StatusBadgeClass => IsPresent
            ? (IsLateArrival ? "badge-late" : "badge-present")
            : (IsExcused ? "badge-excused" : "badge-absent");

        public string StatusIcon => IsPresent
            ? (IsLateArrival ? "bi-clock-history" : "bi-check-circle-fill")
            : (IsExcused ? "bi-shield-exclamation" : "bi-x-circle-fill");

        public string FormattedDate => Date.ToString("yyyy/MM/dd");
        public string DayName => Date.ToString("dddd", new CultureInfo("ar-EG"));
        public string MonthKey => Date.ToString("yyyy-MM");
    }

    public class CourseAttendanceSummaryVM
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; } = "";
        public int Total { get; set; }
        public int Attended { get; set; }
        public int Absent { get; set; }
        public int Late { get; set; }

        public double Rate => Total == 0 ? 0 : Math.Round((double)Attended / Total * 100, 1);
        public string RateColor => Rate switch { >= 85 => "success", >= 70 => "warning", _ => "danger" };
    }

    public class MonthlyAttendanceVM
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthLabel { get; set; } = "";
        public int Total { get; set; }
        public int Attended { get; set; }
        public int Absent { get; set; }

        public double Rate => Total == 0 ? 0 : Math.Round((double)Attended / Total * 100, 1);
    }

    public class AIAttendanceRecommendationVM
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Type { get; set; } = "info"; // success | warning | danger | info
        public string Icon { get; set; } = "bi-lightbulb";
        public int Priority { get; set; }
    }
}
