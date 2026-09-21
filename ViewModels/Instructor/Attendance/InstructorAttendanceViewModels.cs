using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Instructor.Attendance
{
    // =====================================================
    // Index = صفحة دفعات المدرب فقط
    // =====================================================
    public class InstructorAttendanceIndexVM
    {
        public int TotalBatches { get; set; }
        public int TotalLectures { get; set; }
        public int TodayLectures { get; set; }
        public int CompletedAttendanceLectures { get; set; }
        public int PendingAttendanceLectures { get; set; }

        public List<InstructorAttendanceBatchCardVM> BatchCards { get; set; } = new();

        // توافق مع أي صفحات قديمة
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int? BatchId { get; set; }
        public List<InstructorAttendanceBatchOptionVM> Batches { get; set; } = new();
        public List<InstructorLectureAttendanceRowVM> Lectures { get; set; } = new();
    }

    public class InstructorAttendanceBatchCardVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;

        public int TotalStudents { get; set; }
        public int TotalLectures { get; set; }
        public int TodayLectures { get; set; }

        public int RecordedLectures { get; set; }
        public int PendingLectures { get; set; }

        public int PresentCount { get; set; }
        public int LateArrivalCount { get; set; }
        public int EarlyLeavePermissionCount { get; set; }

        public double AttendancePercentage { get; set; }

        public DateTime? LastLectureDate { get; set; }
    }

    public class InstructorAttendanceBatchOptionVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
    }

    // =====================================================
    // Lectures = صفحة محاضرات دفعة محددة
    // =====================================================
    public class InstructorBatchLecturesVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;

        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        public int TotalStudents { get; set; }
        public int TotalLectures { get; set; }
        public int TodayLectures { get; set; }
        public int CompletedAttendanceLectures { get; set; }
        public int PendingAttendanceLectures { get; set; }

        public List<InstructorLectureAttendanceRowVM> Lectures { get; set; } = new();
    }

    public class InstructorLectureAttendanceRowVM
    {
        public int LectureId { get; set; }

        public string Title { get; set; } = string.Empty;
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
    // Record / Report = تسجيل حضور محاضرة واحدة
    // =====================================================
    public class MarkInstructorAttendanceVM
    {
        public int LectureId { get; set; }
        public int BatchId { get; set; }

        public string LectureTitle { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string SectionTitle { get; set; } = string.Empty;
        public string? Location { get; set; }

        public DateTime LectureDate { get; set; }

        public List<InstructorAttendanceStudentRowVM> Students { get; set; } = new();
    }

    public class InstructorAttendanceStudentRowVM
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

    // توافق مع أسماء قديمة لو مستخدمة في ملفات سابقة
    public class InstructorAttendanceLecturesVM : InstructorAttendanceIndexVM
    {
    }

    public class InstructorAttendanceLectureRowVM : InstructorLectureAttendanceRowVM
    {
    }

    public class InstructorRecordAttendanceVM : MarkInstructorAttendanceVM
    {
    }

    public class InstructorAttendanceStudentInputVM : InstructorAttendanceStudentRowVM
    {
    }
}