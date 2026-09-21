using System;
using System.Collections.Generic;
using QdratNew.Enums;

namespace QdratNew.ViewModels.Instructor
{
    public class InstructorDetailsDashboardViewModel
    {
        public int Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string NationalID { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public string? WhatsAppNumber { get; set; }

        public string Specialization { get; set; } = string.Empty;

        public GenderType Gender { get; set; }

        public bool IsActive { get; set; }

        public string? UserId { get; set; }

        public string AvatarText { get; set; } = "مد";

        public string StatusText { get; set; } = "نشط";

        public string GenderText { get; set; } = "غير محدد";

        public double ActivityScore { get; set; }

        public string ActivityLabel { get; set; } = "غير محدد";

        public string ActivityCssClass { get; set; } = "wn";

        public int BatchesCount { get; set; }

        public int StudentsCount { get; set; }

        public int LecturesThisMonth { get; set; }

        public int LecturesThisWeek { get; set; }

        public int ExamsCreatedCount { get; set; }

        public int HomeworksCreatedCount { get; set; }

        public double AttendancePercent { get; set; }

        public double ExamAverageScore { get; set; }

        public double HomeworkSubmissionPercent { get; set; }

        public double ParticipationPercent { get; set; }

        public string AttendanceDeltaText { get; set; } = "لا توجد مقارنة متاحة";

        public string ExamDeltaText { get; set; } = "لا توجد مقارنة متاحة";

        public string HomeworkDeltaText { get; set; } = "لا توجد مقارنة متاحة";

        public List<InstructorBatchPerformanceVm> Batches { get; set; } = new();

        public List<InstructorLectureVm> RecentLectures { get; set; } = new();

        public List<InstructorLectureVm> UpcomingLectures { get; set; } = new();

        public List<InstructorExamImpactVm> Exams { get; set; } = new();

        public List<InstructorHomeworkImpactVm> Homeworks { get; set; } = new();

        public List<InstructorStudentWatchVm> TopStudents { get; set; } = new();

        public List<InstructorStudentWatchVm> RiskStudents { get; set; } = new();

        public List<InstructorDiagnosisItemVm> DiagnosisItems { get; set; } = new();

        public List<InstructorProgressBarVm> ProgressBars { get; set; } = new();

        public List<InstructorHeatmapWeekVm> HeatmapWeeks { get; set; } = new();
    }

    public class InstructorBatchPerformanceVm
    {
        public int BatchId { get; set; }

        public string BatchName { get; set; } = string.Empty;

        public int StudentsCount { get; set; }

        public int LecturesCount { get; set; }

        public double AttendancePercent { get; set; }

        public double ExamAverageScore { get; set; }

        public double HealthScore { get; set; }

        public string HealthCssClass { get; set; } = "health-good";

        public string DecisionHint { get; set; } = "لا يلزم تدخل الآن";
    }

    public class InstructorLectureVm
    {
        public int LectureId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string BatchName { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public int PresentCount { get; set; }

        public int TotalCount { get; set; }

        public double AttendancePercent { get; set; }

        public string StatusText { get; set; } = "مجدولة";

        public string StatusCssClass { get; set; } = "scheduled";
    }

    public class InstructorExamImpactVm
    {
        public int ExamAssignmentId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string BatchName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? ScheduledDate { get; set; }

        public int SubmittedCount { get; set; }

        public int AssignedCount { get; set; }

        public double SubmissionPercent { get; set; }

        public double AverageScore { get; set; }

        public string StatusText { get; set; } = "غير محدد";

        public string StatusCssClass { get; set; } = "neutral";
    }

    public class InstructorHomeworkImpactVm
    {
        public int HomeworkSetId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string BatchName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? EndAt { get; set; }

        public int SubmittedCount { get; set; }

        public int AssignedCount { get; set; }

        public double SubmissionPercent { get; set; }

        public double AverageScore { get; set; }

        public string StatusText { get; set; } = "غير محدد";

        public string StatusCssClass { get; set; } = "neutral";
    }

    public class InstructorStudentWatchVm
    {
        public int StudentId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string BatchName { get; set; } = string.Empty;

        public string AvatarText { get; set; } = "ط";

        public int Rank { get; set; }

        public double AttendancePercent { get; set; }

        public double ExamAverageScore { get; set; }

        public double HomeworkSubmissionPercent { get; set; }

        public double FinalScore { get; set; }

        public string TrendCssClass { get; set; } = "eq";

        public string TrendText { get; set; } = "ثابت";

        public List<string> Flags { get; set; } = new();
    }

    public class InstructorDiagnosisItemVm
    {
        public string CssClass { get; set; } = "opp";

        public string Icon { get; set; } = "🎯";

        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;
    }

    public class InstructorProgressBarVm
    {
        public string Label { get; set; } = string.Empty;

        public double Value { get; set; }

        public double Target { get; set; }

        public string CssColor { get; set; } = "var(--blue)";
    }

    public class InstructorHeatmapWeekVm
    {
        public List<InstructorHeatmapDayVm> Days { get; set; } = new();
    }

    public class InstructorHeatmapDayVm
    {
        public int ActivityCount { get; set; }

        public string CssStyle { get; set; } = "background:var(--bg-2);color:var(--mute)";
    }
}