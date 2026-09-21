using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Users
{
    public class UserDetailsViewModel
    {
        // ── بيانات المستخدم الأساسية ──────────────────────────────
        public string UserId         { get; set; } = "";
        public string FullName       { get; set; } = "";
        public string Email          { get; set; } = "";
        public string? PhoneNumber   { get; set; }
        public string? WhatsAppNumber{ get; set; }
        public bool   IsActive       { get; set; }
        public string? ProfileImagePath { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime? JoinedAt    { get; set; }
        public List<string> Roles    { get; set; } = new();

        // ── طالب أم لا ───────────────────────────────────────────
        public bool   IsStudent      { get; set; }
        public int?   StudentId      { get; set; }
        public string? StudentStatus { get; set; }
        public string? Level         { get; set; }

        // نسبة التقدير العام (تُحسب من متوسط درجات الواجبات)
        public double OverallGradePercent { get; set; }
        public string GradeLabel =>
            OverallGradePercent >= 90 ? "ممتاز" :
            OverallGradePercent >= 75 ? "جيد جداً" :
            OverallGradePercent >= 65 ? "جيد" :
            OverallGradePercent >= 50 ? "مقبول" : "ضعيف";

        // ── إحصائيات سريعة ───────────────────────────────────────
        public int    TotalLoginCount       { get; set; }
        public int    CompletedCoursesCount { get; set; }
        public int    SubmittedAssignments  { get; set; }
        public double AverageExamScore      { get; set; }

        // ── الدفعة الحالية (للشريط الجانبي) ─────────────────────
        public string? CurrentBatchName           { get; set; }
        public int     CurrentBatchLecturesCount  { get; set; }
        public int     EnrolledCoursesCount       { get; set; }

        // ── بيانات التبويبات ──────────────────────────────────────
        public List<UserBatchDto>      Batches        { get; set; } = new();
        public List<UserCourseDto>     Courses        { get; set; } = new();
        public List<UserLoginLogDto>   LoginLogs      { get; set; } = new();
        public List<UserAttendanceDto> Attendance     { get; set; } = new();
        public List<UserAssignmentDto> Assignments    { get; set; } = new();
        public List<UserExamDto>       Exams          { get; set; } = new();

        // ── ملخص الحضور ──────────────────────────────────────────
        public int AttendedCount { get; set; }
        public int AbsentCount   { get; set; }
        public int LateCount     { get; set; }
        public double AttendancePercent =>
            (AttendedCount + AbsentCount + LateCount) > 0
                ? Math.Round((double)AttendedCount / (AttendedCount + AbsentCount + LateCount) * 100, 1)
                : 0;

        public string NationalID { get; internal set; }
        public string? EnrollmentStatus { get; internal set; }
        public object ActivityLogs { get; internal set; }
        public int SubmittedHomeworkCount { get; internal set; }
    }

    // ─────────────────────────────────────────────────────────────
    // DTOs التبويبات
    // ─────────────────────────────────────────────────────────────

    public class UserBatchDto
    {
        public int      BatchId      { get; set; }
        public int      CourseId     { get; set; }
        public string   BatchName    { get; set; } = "";
        public string   CourseName   { get; set; } = "";
        public DateTime StartDate    { get; set; }
        public DateTime? EndDate     { get; set; }
        public string   Status       { get; set; } = "";
        public DateTime EnrolledAt   { get; set; }
        public bool     IsArchived   { get; set; }
        public DateTime? BatchStartDate { get; internal set; }
        public DateTime? BatchEndDate { get; internal set; }
    }

    public class UserCourseDto
    {
        public int     CourseId      { get; set; }
        public string  CourseName    { get; set; } = "";
        public string  BatchName     { get; set; } = "";
        public int     TotalLectures { get; set; }
        public int     WatchedCount  { get; set; }
        public double  ProgressPct   => TotalLectures > 0
            ? Math.Round((double)WatchedCount / TotalLectures * 100, 1) : 0;
        public bool    IsCompleted   { get; set; }
        public DateTime? LastActivity { get; set; }
        public float? Grade { get; internal set; }
    }

    public class UserLoginLogDto
    {
        public DateTime  LoginAt    { get; set; }
        public DateTime? LogoutAt   { get; set; }
        public string?   DeviceInfo { get; set; }
        public string?   IpAddress  { get; set; }
        public TimeSpan? Duration   => LogoutAt.HasValue ? LogoutAt - LoginAt : null;
    }

    public class UserAttendanceDto
    {
        public int      Id           { get; set; }
        public DateTime Date         { get; set; }
        public string   LectureName  { get; set; } = "";
        public string   CourseName   { get; set; } = "";
        public DateTime? CheckIn     { get; set; }
        public DateTime? CheckOut    { get; set; }
        public string   Status       { get; set; } = "";
        public string?  Notes        { get; set; }
    }

    public class UserAssignmentDto
    {
        public int      HomeworkSetId  { get; set; }
        public string   Title          { get; set; } = "";
        public string   CourseName     { get; set; } = "";
        public DateTime? DueDate       { get; set; }
        public string   Status         { get; set; } = "";
        public double?  Score          { get; set; }
        public int      TotalQuestions { get; set; }
        public int      Correct        { get; set; }
        public DateTime? SubmittedAt   { get; set; }
        public string? BatchName { get; internal set; }
        public string? LectureName { get; internal set; }
        public DateTime AssignedAt { get; internal set; }
        public bool IsCompleted { get; internal set; }
    }

    public class UserExamDto
    {
        public int      ExamId       { get; set; }
        public string   ExamTitle    { get; set; } = "";
        public string   CourseName   { get; set; } = "";
        public DateTime? TakenAt     { get; set; }
        public double   ScorePercent { get; set; }
        public int      Total        { get; set; }
        public int      Correct      { get; set; }
        public string   Result       =>
            ScorePercent >= 90 ? "ممتاز" :
            ScorePercent >= 75 ? "جيد جداً" :
            ScorePercent >= 65 ? "جيد" :
            ScorePercent >= 50 ? "مقبول" : "راسب";
        public string ResultClass    =>
            ScorePercent >= 65 ? "success" :
            ScorePercent >= 50 ? "warning" : "danger";
    }
}
