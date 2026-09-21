using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Students
{
    public class StudentDetailsDashboardViewModel
    {
        public int StudentID { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string NationalID { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        public string? WhatsAppNumber { get; set; }

        public string Gender { get; set; } = string.Empty;

        public string? School { get; set; }

        public string? Level { get; set; }

        public string EnrollmentStatus { get; set; } = string.Empty;

        public string BranchName { get; set; } = "غير محدد";

        public string ParentName { get; set; } = "غير محدد";

        public DateTime RegistrationDate { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public string AvatarText { get; set; } = "ط";

        public string StatusText { get; set; } = "نشط";

        public double LearningHealthScore { get; set; }

        public string LearningHealthLabel { get; set; } = "غير محدد";

        public string LearningHealthCssClass { get; set; } = "wn";

        public int BatchesCount { get; set; }

        public int AttendanceRecordsCount { get; set; }

        public double AttendancePercent { get; set; }

        public double HomeworkSubmissionPercent { get; set; }

        public double ExamAverageScore { get; set; }

        public double CorrectAnswerPercent { get; set; }

        public int HomeworksAssignedCount { get; set; }

        public int HomeworksSubmittedCount { get; set; }

        public int HomeworksMissingCount { get; set; }

        public int ExamsAssignedCount { get; set; }

        public int ExamsSubmittedCount { get; set; }

        public int QuestionAttemptsCount { get; set; }

        public int CorrectQuestionAttemptsCount { get; set; }

        public int RemedialPlansCount { get; set; }

        public int CompletedRemedialPlansCount { get; set; }

        public double StudyProgressPercent { get; set; }

        public string AttendanceDeltaText { get; set; } = "لا توجد مقارنة متاحة";

        public string HomeworkDeltaText { get; set; } = "لا توجد مقارنة متاحة";

        public string ExamDeltaText { get; set; } = "لا توجد مقارنة متاحة";

        public string CorrectAnswerDeltaText { get; set; } = "لا توجد مقارنة متاحة";

        public List<StudentBatchDetailsVm> Batches { get; set; } = new();

        public List<StudentAttendanceDayVm> AttendanceCalendar { get; set; } = new();

        public List<StudentHomeworkDetailsVm> Homeworks { get; set; } = new();

        public List<StudentExamDetailsVm> Exams { get; set; } = new();

        public List<StudentPerformanceDetailsVm> Performances { get; set; } = new();

        public List<StudentRemedialPlanDetailsVm> RemedialPlans { get; set; } = new();

        public List<StudentWeaknessSectionVm> WeaknessSections { get; set; } = new();

        public List<StudentDiagnosisItemVm> DiagnosisItems { get; set; } = new();

        public List<StudentProgressBarVm> ProgressBars { get; set; } = new();

        public List<StudentHeatmapWeekVm> HeatmapWeeks { get; set; } = new();
    }

    public class StudentBatchDetailsVm
    {
        public int BatchId { get; set; }

        public string BatchName { get; set; } = string.Empty;

        public string CourseName { get; set; } = "غير محدد";

        public string BranchName { get; set; } = "غير محدد";

        public DateTime EnrolledAt { get; set; }

        public string Status { get; set; } = string.Empty;

        public double AttendancePercent { get; set; }

        public double ExamAverageScore { get; set; }

        public double HomeworkSubmissionPercent { get; set; }

        public double BatchStudentHealthScore { get; set; }

        public string HealthCssClass { get; set; } = "health-good";

        public string DecisionHint { get; set; } = "لا يلزم تدخل الآن";
    }

    public class StudentAttendanceDayVm
    {
        public DateTime Date { get; set; }

        public string DayText { get; set; } = string.Empty;

        public bool HasLecture { get; set; }

        public bool IsPresent { get; set; }

        public bool IsToday { get; set; }

        public string CssClass { get; set; } = "no-lecture";
    }

    public class StudentHomeworkDetailsVm
    {
        public int HomeworkSetId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string BatchName { get; set; } = string.Empty;

        public DateTime AssignedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public DateTime? EndAt { get; set; }

        public bool IsSubmitted { get; set; }

        public double? Score { get; set; }

        public string StatusText { get; set; } = "غير محدد";

        public string StatusCssClass { get; set; } = "neutral";
    }

    public class StudentExamDetailsVm
    {
        public int ExamStudentStatusId { get; set; }

        public int? ExamAssignmentId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string BatchName { get; set; } = string.Empty;

        public DateTime AssignedAt { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public bool IsSubmitted { get; set; }

        public int? Score { get; set; }

        public string StatusText { get; set; } = "غير محدد";

        public string StatusCssClass { get; set; } = "neutral";
    }

    public class StudentPerformanceDetailsVm
    {
        public int Id { get; set; }

        public DateTime ExamDate { get; set; }

        public double Score { get; set; }

        public string Level { get; set; } = string.Empty;

        public string? WeakTopics { get; set; }

        public double EngagementScore { get; set; }

        public string CssClass { get; set; } = "neutral";
    }

    public class StudentRemedialPlanDetailsVm
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string PerformanceLevel { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public int TotalLessons { get; set; }

        public int CompletedLessons { get; set; }

        public double CompletionPercent { get; set; }

        public bool IsCompleted { get; set; }

        public string StatusText { get; set; } = "غير مكتملة";

        public string StatusCssClass { get; set; } = "warning";
    }

    public class StudentWeaknessSectionVm
    {
        public int? SectionId { get; set; }

        public int WrongAttemptsCount { get; set; }

        public int TotalAttemptsCount { get; set; }

        public double WrongPercent { get; set; }

        public string Title { get; set; } = "محور غير محدد";

        public string CssClass { get; set; } = "warning";
    }

    public class StudentDiagnosisItemVm
    {
        public string CssClass { get; set; } = "opp";

        public string Icon { get; set; } = "🎯";

        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;
    }

    public class StudentProgressBarVm
    {
        public string Label { get; set; } = string.Empty;

        public double Value { get; set; }

        public double Target { get; set; }

        public string CssColor { get; set; } = "var(--blue)";
    }

    public class StudentHeatmapWeekVm
    {
        public List<StudentHeatmapDayVm> Days { get; set; } = new();
    }

    public class StudentHeatmapDayVm
    {
        public int ActivityCount { get; set; }

        public string CssStyle { get; set; } = "background:var(--bg-2);color:var(--mute)";
    }
}