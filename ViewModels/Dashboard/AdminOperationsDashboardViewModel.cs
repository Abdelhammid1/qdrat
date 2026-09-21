namespace QdratNew.ViewModels.Dashboard
{

    public class AdminOperationsDashboardViewModel
    {
        public AdminDashboardKpiViewModel Kpis { get; set; } = new AdminDashboardKpiViewModel();

        public List<LectureTimelineItemViewModel> LectureTimeline { get; set; } = new List<LectureTimelineItemViewModel>();

        public List<LiveAttendanceRowViewModel> LiveAttendance { get; set; } = new List<LiveAttendanceRowViewModel>();

        public List<BatchPerformanceMatrixRowViewModel> BatchPerformanceMatrix { get; set; } = new List<BatchPerformanceMatrixRowViewModel>();

        public List<InstructorPerformanceRowViewModel> InstructorPerformance { get; set; } = new List<InstructorPerformanceRowViewModel>();

        public List<PredictiveCalendarDayViewModel> PredictiveCalendar { get; set; } = new List<PredictiveCalendarDayViewModel>();

        public List<SmartAlertViewModel> SmartAlerts { get; set; } = new List<SmartAlertViewModel>();

        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }

    public class AdminDashboardKpiViewModel
    {
        public int ActiveStudentsToday { get; set; }

        public int RunningLecturesNow { get; set; }

        public int OpenExamsNow { get; set; }

        public double AttendancePercentToday { get; set; }

        public int HomeworksClosingWithin24Hours { get; set; }

        public int CompletedLecturesToday { get; set; }

        public int LowAttendanceLecturesToday { get; set; }

        public int AbsentAllDayStudentsCount { get; set; }

        public int ActiveStudentsYesterday { get; set; }

        public double AttendancePercentYesterday { get; set; }

        public int RunningLecturesYesterday { get; set; }

        public int OpenExamsYesterday { get; set; }

        public int HomeworksClosingYesterday { get; set; }

        public string ActiveStudentsTrendText { get; set; } = "مقارنة بالأمس غير متاحة";

        public string AttendanceTrendText { get; set; } = "مقارنة بالأمس غير متاحة";

        public string RunningLecturesTrendText { get; set; } = "مقارنة بالأمس غير متاحة";

        public string OpenExamsTrendText { get; set; } = "مقارنة بالأمس غير متاحة";

        public string HomeworksClosingTrendText { get; set; } = "مقارنة بالأمس غير متاحة";
    }

    public class LectureTimelineItemViewModel
    {
        public int LectureId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string BatchName { get; set; } = string.Empty;

        public string InstructorName { get; set; } = string.Empty;

        public DateTime StartAt { get; set; }

        public DateTime EndAt { get; set; }

        public int StartMinuteOfDay { get; set; }

        public int DurationMinutes { get; set; }

        public string StatusCode { get; set; } = "scheduled";

        public string StatusText { get; set; } = "مقررة";

        public double AttendancePercent { get; set; }

        public int PresentStudents { get; set; }

        public int TotalStudents { get; set; }

        public bool IsUrgent { get; set; }
    }

    public class LiveAttendanceRowViewModel
    {
        public int LectureId { get; set; }

        public string LectureTitle { get; set; } = string.Empty;

        public string BatchName { get; set; } = string.Empty;

        public string InstructorName { get; set; } = string.Empty;

        public int ConnectedStudents { get; set; }

        public int TotalStudents { get; set; }

        public double AttendancePercent { get; set; }

        public DateTime StartAt { get; set; }

        public string StatusText { get; set; } = string.Empty;

        public bool CanWarnInstructor { get; set; }

        public string ActionText { get; set; } = "لا يلزم";

        public string UrgencyCssClass { get; set; } = "normal";
    }

    public class BatchPerformanceMatrixRowViewModel
    {
        public int BatchId { get; set; }

        public string BatchName { get; set; } = string.Empty;

        public bool IsPartner { get; set; }

        public int StudentsCount { get; set; }

        public double AttendancePercentToday { get; set; }

        public double HomeworkSubmissionPercent { get; set; }

        public double ExamAverageScore { get; set; }

        public double HealthScore { get; set; }

        public string HealthStatusText { get; set; } = string.Empty;

        public string HealthCssClass { get; set; } = string.Empty;

        public string DecisionHint { get; set; } = string.Empty;
    }

    public class InstructorPerformanceRowViewModel
    {
        public int InstructorId { get; set; }

        public string InstructorName { get; set; } = string.Empty;

        public int LecturesThisWeek { get; set; }

        public int HomeworksThisWeek { get; set; }

        public int ExamsThisWeek { get; set; }

        public double StudentParticipationPercent { get; set; }

        public double ActivityScore { get; set; }

        public string StatusText { get; set; } = string.Empty;

        public string StatusCssClass { get; set; } = string.Empty;

        public string ActionHint { get; set; } = string.Empty;
    }

    public class PredictiveCalendarDayViewModel
    {
        public DateTime Date { get; set; }

        public string DayName { get; set; } = string.Empty;

        public int LecturesCount { get; set; }

        public int ExamsCount { get; set; }

        public int HomeworksClosingCount { get; set; }

        public bool HasDensityWarning { get; set; }

        public string WarningText { get; set; } = string.Empty;
    }

    public class SmartAlertViewModel
    {
        public string Level { get; set; } = "info";

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string SuggestedActionText { get; set; } = string.Empty;

        public string TargetUrl { get; set; } = "#";

        public string ContextLabel { get; set; } = string.Empty;

        public bool IsUrgent { get; set; }
    }

    public class AdminDashboardPulseViewModel
    {
        public AdminDashboardKpiViewModel Kpis { get; set; } = new AdminDashboardKpiViewModel();

        public List<LiveAttendanceRowViewModel> LiveAttendance { get; set; } = new List<LiveAttendanceRowViewModel>();

        public List<SmartAlertViewModel> SmartAlerts { get; set; } = new List<SmartAlertViewModel>();

        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }


}
