using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Batch
{
    public class BatchDetailsDashboardViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string CourseName { get; set; } = "غير محدد";

        public string BranchName { get; set; } = "غير محدد";

        public string Gender { get; set; } = "";

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string AvatarText { get; set; } = "دف";

        public string StatusText { get; set; } = "نشطة";

        public string StageText { get; set; } = "مرحلة غير محددة";

        public string Description { get; set; } = string.Empty;

        public int StudentsCount { get; set; }

        public int InstructorsCount { get; set; }

        public double AttendancePercent { get; set; }

        public double HomeworkSubmissionPercent { get; set; }

        public double ExamAverageScore { get; set; }

        public int CompletedLessonsCount { get; set; }

        public int TotalLessonsCount { get; set; }

        public int DaysRemaining { get; set; }

        public double CourseProgressPercent { get; set; }

        public double HealthScore { get; set; }

        public string HealthLabel { get; set; } = "غير محدد";

        public string HealthCssClass { get; set; } = "wn";

        public string AttendanceDeltaText { get; set; } = "لا توجد مقارنة متاحة";

        public string HomeworkDeltaText { get; set; } = "لا توجد مقارنة متاحة";

        public string ExamDeltaText { get; set; } = "لا توجد مقارنة متاحة";

        public List<BatchLessonProgressVm> Lessons { get; set; } = new();

        public List<BatchHomeworkDetailsVm> Homeworks { get; set; } = new();

        public List<BatchInstructorDetailsVm> Instructors { get; set; } = new();

        public List<BatchStudentPerformanceVm> TopStudents { get; set; } = new();

        public List<BatchStudentPerformanceVm> RiskStudents { get; set; } = new();

        public List<BatchDiagnosisItemVm> DiagnosisItems { get; set; } = new();

        public List<BatchProgressBarVm> ProgressBars { get; set; } = new();

        public List<BatchAttendanceDayVm> AttendanceCalendar { get; set; } = new();

        public List<BatchHeatmapWeekVm> HeatmapWeeks { get; set; } = new();
    }

    public class BatchLessonProgressVm
    {
        public int LessonId { get; set; }

        public int Number { get; set; }

        public string Title { get; set; } = string.Empty;

        public string StatusText { get; set; } = "قادم";

        public string CssClass { get; set; } = "pending";

        public double AttendancePercent { get; set; }

        public bool HasHomework { get; set; }

        public bool HasExam { get; set; }
    }

    public class BatchHomeworkDetailsVm
    {
        public int HomeworkSetId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string InstructorName { get; set; } = "غير محدد";

        public DateTime CreatedAt { get; set; }

        public DateTime? EndAt { get; set; }

        public string StatusText { get; set; } = "غير محدد";

        public string StatusCssClass { get; set; } = "closed";

        public int ExpectedCount { get; set; }

        public int SubmittedCount { get; set; }

        public int MissingCount { get; set; }

        public double SubmissionPercent { get; set; }

        public double AverageScore { get; set; }

        public string MissingCssClass { get; set; } = "none";
    }

    public class BatchInstructorDetailsVm
    {
        public int InstructorId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Specialization { get; set; } = "مدرب";

        public string AvatarText { get; set; } = "مد";

        public int LecturesThisWeek { get; set; }

        public int HomeworksCount { get; set; }

        public int ExamsCount { get; set; }

        public double StudentParticipationPercent { get; set; }

        public double ActivityScore { get; set; }

        public string ActivityCssColor { get; set; } = "var(--blue)";
    }

    public class BatchStudentPerformanceVm
    {
        public int StudentId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string AvatarText { get; set; } = "ط";

        public int Rank { get; set; }

        public double AttendancePercent { get; set; }

        public double HomeworkSubmissionPercent { get; set; }

        public double AverageScore { get; set; }

        public int MissingHomeworksCount { get; set; }

        public string TrendCssClass { get; set; } = "eq";

        public string TrendText { get; set; } = "ثابت";

        public List<string> Flags { get; set; } = new();
    }

    public class BatchDiagnosisItemVm
    {
        public string CssClass { get; set; } = "opp";

        public string Icon { get; set; } = "🎯";

        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;
    }

    public class BatchProgressBarVm
    {
        public string Label { get; set; } = string.Empty;

        public double Value { get; set; }

        public double Target { get; set; }

        public string CssColor { get; set; } = "var(--blue)";
    }

    public class BatchAttendanceDayVm
    {
        public DateTime Date { get; set; }

        public string DayText { get; set; } = string.Empty;

        public double Percent { get; set; }

        public string CssClass { get; set; } = "no-lecture";

        public bool HasLecture { get; set; }

        public bool IsToday { get; set; }
    }

    public class BatchHeatmapWeekVm
    {
        public List<BatchHeatmapDayVm> Days { get; set; } = new();
    }

    public class BatchHeatmapDayVm
    {
        public int ActivityCount { get; set; }

        public string CssStyle { get; set; } = "background:var(--bg-2);color:var(--mute)";
    }
}