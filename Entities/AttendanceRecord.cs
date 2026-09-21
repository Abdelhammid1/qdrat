using System;
using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class AttendanceRecord
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; }

        public int LectureId { get; set; }  // ✅ بدون nullable
        public Lecture Lecture { get; set; }


        public bool IsPresent { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.Now;

        public string? Notes { get; set; }

        public int? ExamAssignmentId { get; set; } // ✅ الجديد
        public ExamAssignmentToBatch ExamAssignment { get; set; }
        // ✅ جديد: ربط مع الجلسة العلاجية
        public int? RemedialSessionId { get; set; }
        public RemedialSession? RemedialSession { get; set; }





        // =====================================================
        // Instructor Attendance Analytics Fields
        // =====================================================
        public bool IsLateArrival { get; set; } = false;
        public bool HasEarlyLeavePermission { get; set; } = false;
        public TimeSpan? ActualArrivalTime { get; set; }
        public TimeSpan? ActualDepartureTime { get; set; }
        public DateTime? EarlyLeavePermissionAt { get; set; }




    }
}
