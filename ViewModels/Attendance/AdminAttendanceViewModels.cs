using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Attendance
{
    // =====================================================
    // Index = دفعات الحضور والانصراف للأدمن
    // =====================================================
    public class AdminAttendanceIndexVM
    {
        public int? InstructorId { get; set; }

        public int TotalBatches { get; set; }
        public int TotalStudents { get; set; }
        public int TotalLectures { get; set; }
        public int TodayLectures { get; set; }
        public int CompletedAttendanceLectures { get; set; }
        public int PendingAttendanceLectures { get; set; }
        public int ArchivedBatchCount { get; set; }

        public List<SelectListItem> Instructors { get; set; } = new();
        public List<SelectListItem> AllBatchesForArchive { get; set; } = new();
        public List<AdminAttendanceBatchCardVM> BatchCards { get; set; } = new();
        public bool ShowArchived { get; set; }
    }

    // =====================================================
    // ArchivedAttendance = صفحة أرشيف الحضور المستقلة
    // =====================================================
    public class AttendanceArchivedIndexVM
    {
        public int TotalArchivedBatches { get; set; }
        public int TotalStudents { get; set; }
        public int TotalLectures { get; set; }
        public double AverageAttendancePercentage { get; set; }
        public List<AdminAttendanceBatchCardVM> BatchCards { get; set; } = new();
    }

    public class AdminAttendanceBatchCardVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public bool IsArchived { get; set; }

        public int TotalStudents { get; set; }
        public int TotalLectures { get; set; }
        public int TodayLectures { get; set; }

        public int RecordedLectures { get; set; }
        public int PendingLectures { get; set; }

        public int PresentCount { get; set; }
        public int LateArrivalCount { get; set; }
        public int EarlyLeavePermissionCount { get; set; }

        public double AttendancePercentage { get; set; }

        public string InstructorSummary { get; set; } = string.Empty;
        public DateTime? LastLectureDate { get; set; }
    }

    // =====================================================
    // Lectures = محاضرات دفعة محددة
    // =====================================================
    public class AdminBatchLecturesVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;

        public int? InstructorId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        public int TotalStudents { get; set; }
        public int TotalLectures { get; set; }
        public int TodayLectures { get; set; }
        public int CompletedAttendanceLectures { get; set; }
        public int PendingAttendanceLectures { get; set; }

        public List<SelectListItem> Instructors { get; set; } = new();
        public List<AdminLectureAttendanceRowVM> Lectures { get; set; } = new();
        public List<AttendanceBatchStudentCardVM> Students { get; set; } = new();
        public List<AttCurriculumInstructorVM> CurriculumInstructors { get; set; } = new();
    }

    public class AdminLectureAttendanceRowVM
    {
        public int LectureId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string SectionTitle { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        public DateTime Date { get; set; }
        public bool IsToday { get; set; }

        public int TotalStudents { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int LateArrivalCount { get; set; }
        public int EarlyLeavePermissionCount { get; set; }

        public bool HasAttendance { get; set; }
        public double AttendancePercentage { get; set; }
    }

    // =====================================================
    // Record / LectureReport
    // =====================================================
    public class AdminRecordAttendanceVM
    {
        public int LectureId { get; set; }
        public int BatchId { get; set; }

        public string LectureTitle { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string SectionTitle { get; set; } = string.Empty;
        public string? Location { get; set; }

        public DateTime LectureDate { get; set; }

        public List<AdminAttendanceStudentRowVM> Students { get; set; } = new();
    }

    public class AdminAttendanceStudentRowVM
    {
        public int StudentId { get; set; }

        public string StudentName { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;

        public bool IsPresent { get; set; }
        public bool IsLateArrival { get; set; }
        public bool HasEarlyLeavePermission { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? ActualArrivalTime { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? ActualDepartureTime { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    // =====================================================
    // Student offcanvas data for Lectures page
    // =====================================================
    public class AttendanceBatchStudentCardVM
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public int AttendedCount { get; set; }
        public int TotalLectures { get; set; }
        public int SolvedHomeworks { get; set; }
        public int TotalHomeworks { get; set; }
        public List<AttStudentLectureVM> Lectures { get; set; } = new();
        public List<AttStudentHwVM> Homeworks { get; set; } = new();
    }

    public class AttStudentLectureVM
    {
        public string Title { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string MonthLabel { get; set; } = string.Empty;
        public bool HasRecord { get; set; }
        public bool IsPresent { get; set; }
        public bool IsLate { get; set; }
    }

    public class AttStudentHwVM
    {
        public string Title { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public bool IsSolved { get; set; }
        public double? Score { get; set; }
        public bool StudentContacted { get; set; }
        public bool ParentContacted { get; set; }
        public int? CurriculumId { get; set; }
        public string? CurriculumTitle { get; set; }
    }

    public class AttCurriculumInstructorVM
    {
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
    }
}