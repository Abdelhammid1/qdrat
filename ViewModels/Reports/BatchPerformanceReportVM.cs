using QdratNew.ViewModels.Homework;

namespace QdratNew.ViewModels.Reports
{
    public class BatchPerformanceReportVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseTitle { get; set; } = "";

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public bool IsYesterdayOnly { get; set; }

        public int TotalStudents { get; set; }

        // ── المحاضرات والحضور ──
        public List<PerformanceLectureVM> Lectures { get; set; } = new();
        public int TotalLecturesInPeriod { get; set; }
        public double OverallAttendanceRate { get; set; }

        // ── الواجبات ──
        public List<PerformanceHomeworkVM> Homeworks { get; set; } = new();
        public int TotalHomeworksInPeriod { get; set; }
        public double OverallSubmissionRate { get; set; }
        public double OverallAverageScore { get; set; }

        // ── تصنيف الطلاب ──
        public List<StudentPerformanceRowVM> StudentsAbove60 { get; set; } = new();
        public List<StudentPerformanceRowVM> StudentsBelow60 { get; set; } = new();
        public List<StudentPerformanceRowVM> StudentsNotSubmitted { get; set; } = new();

        public int CountAbove60 { get; set; }
        public int CountBelow60 { get; set; }
        public int CountNotSubmitted { get; set; }

        // ── المدرّبون ──
        public List<CurriculumInstructorVM> CurriculumInstructors { get; set; } = new();

        // ── التوصيات ──
        public List<BatchRecommendationVM> Recommendations { get; set; } = new();

        // ── الملخص التنفيذي ──
        public string ExecutiveSummary { get; set; } = "";
        public string OverallStatusLabel { get; set; } = "";
        public string OverallStatusSeverity { get; set; } = "info";

        // ── مقارنة الفترة السابقة ──
        public DateTime PreviousFromDate { get; set; }
        public DateTime PreviousToDate { get; set; }
        public PeriodComparisonVM Comparison { get; set; } = new();

        // ── بيانات الرسوم البيانية ──
        public ChartDataVM AttendanceTrendChart { get; set; } = new();
        public ChartDataVM ScoreDistributionChart { get; set; } = new();
        public ChartDataVM HomeworkSubmissionChart { get; set; } = new();
        public ChartDataVM CurriculumPerformanceChart { get; set; } = new();

        // ── أفضل/أضعف الطلاب ──
        public List<StudentPerformanceRowVM> TopPerformers { get; set; } = new();
        public List<StudentPerformanceRowVM> BottomPerformers { get; set; } = new();

        // ── تفصيل المناهج ──
        public List<CurriculumBreakdownVM> CurriculumBreakdown { get; set; } = new();

        // ── مؤشر الخطورة ──
        public List<StudentRiskRowVM> StudentsRisk { get; set; } = new();

        // ── خطة العمل الأسبوعية ──
        public List<string> WeeklyActionPlan { get; set; } = new();
    }

    public class PerformanceLectureVM
    {
        public int LectureId { get; set; }
        public string Title { get; set; } = "";
        public DateTime Date { get; set; }
        public int PresentCount { get; set; }
        public int LateCount { get; set; }
        public int AbsentCount { get; set; }
        public int TotalStudents { get; set; }
        public double AttendanceRate { get; set; }
    }

    public class PerformanceHomeworkVM
    {
        public int HomeworkSetId { get; set; }
        public string Title { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? EndAt { get; set; }
        public bool IsClosed { get; set; }
        public int TotalAssigned { get; set; }
        public int SubmittedCount { get; set; }
        public int NotSubmittedCount { get; set; }
        public double SubmissionRate { get; set; }
        public double AverageScore { get; set; }
        public int CurriculumId { get; set; }
    }

    public class StudentPerformanceRowVM
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = "";
        public string? PhoneNumber { get; set; }

        public double AverageScore { get; set; }
        public int SubmittedCount { get; set; }
        public int AssignedCount { get; set; }

        public int AttendedCount { get; set; }
        public int TotalLecturesInPeriod { get; set; }

        public List<string> MissingHomeworkTitles { get; set; } = new();
    }

    public class BatchRecommendationVM
    {
        public string Icon { get; set; } = "";
        public string Severity { get; set; } = "info";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public class KpiComparisonVM
    {
        public double CurrentValue { get; set; }
        public double PreviousValue { get; set; }
        public double Delta => Math.Round(CurrentValue - PreviousValue, 1);
        public string Trend => Delta > 0.5 ? "up" : (Delta < -0.5 ? "down" : "flat");
    }

    public class PeriodComparisonVM
    {
        public KpiComparisonVM AttendanceRate { get; set; } = new();
        public KpiComparisonVM SubmissionRate { get; set; } = new();
        public KpiComparisonVM AverageScore { get; set; } = new();
        public KpiComparisonVM CountAbove60 { get; set; } = new();
        public KpiComparisonVM CountBelow60 { get; set; } = new();
        public KpiComparisonVM CountNotSubmitted { get; set; } = new();
    }

    public class ChartDataVM
    {
        public List<string> Labels { get; set; } = new();
        public List<ChartSeriesVM> Series { get; set; } = new();
    }

    public class ChartSeriesVM
    {
        public string Name { get; set; } = "";
        public List<double> Values { get; set; } = new();
    }

    public class CurriculumBreakdownVM
    {
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; } = "";
        public string InstructorName { get; set; } = "";
        public double AverageScore { get; set; }
        public double SubmissionRate { get; set; }
        public int HomeworkCount { get; set; }
    }

    public class StudentRiskRowVM
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public double AverageScore { get; set; }
        public double SubmissionRate { get; set; }
        public double AttendanceRate { get; set; }
        public double AtRiskIndex { get; set; }
        public string RiskLevel { get; set; } = "";
        public string RiskSeverity { get; set; } = "";
    }
}
