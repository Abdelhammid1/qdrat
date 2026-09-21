using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Lecture
{
    public class LectureDetailsViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime Date { get; set; }

        public TimeSpan? ScheduledTime { get; set; }
        public TimeSpan? ScheduledEndTime { get; set; }
        public int? DurationMinutes { get; set; }

        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }
        public string? StartedByRole { get; set; }
        public string? EndedByRole { get; set; }

        public string InstructorName { get; set; } = "غير محدد";
        public string SectionTitle { get; set; } = "غير محدد";
        public string CourseName { get; set; } = "غير محدد";
        public string BatchName { get; set; } = "غير محدد";
        public int BatchId { get; set; }

        public int TotalStudents { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }

        public List<LectureAttendanceStudentRow> AttendanceRecords { get; set; } = new();
        public List<string> LessonTitles { get; set; } = new();

        public bool IsCompleted => ActualEndTime.HasValue;
        public bool IsStarted => ActualStartTime.HasValue;

        public double AttendanceRate =>
            TotalStudents == 0 ? 0 : Math.Round((double)PresentCount / TotalStudents * 100, 1);
    }

    public class LectureAttendanceStudentRow
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public bool IsPresent { get; set; }
        public bool IsLateArrival { get; set; }
        public TimeSpan? ActualArrivalTime { get; set; }
        public string? Notes { get; set; }
        public DateTime RecordedAt { get; set; }
    }
}
