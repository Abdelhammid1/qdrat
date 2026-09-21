using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    public class Lecture
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "العنوان مطلوب")]
        public string Title { get; set; }

        public string Location { get; set; }

        [Required(ErrorMessage = "التاريخ مطلوب")]
        public DateTime Date { get; set; }

        // 🔹 المدرب
        [Required(ErrorMessage = "المدرب مطلوب")]
        public int InstructorId { get; set; }
        public Instructor Instructor { get; set; }

        // 🔹 مصدر تعيين المدرب — يحدد هل هذه المحاضرة تخضع للمزامنة التلقائية مع InstructorCurriculumBatch أم لا
        public LectureInstructorAssignmentSource InstructorAssignmentSource { get; set; } = LectureInstructorAssignmentSource.Auto;

        // 🔹 المحور
        [Required(ErrorMessage = "المحور مطلوب")]
        public int SectionId { get; set; }
        public Section Section { get; set; }

        // 🔹 الدورة
        [Required(ErrorMessage = "الدورة مطلوبة")]
        public int CourseId { get; set; }
        public Course Course { get; set; }

        // 🔹 الدفعة
        [Required(ErrorMessage = "الدفعة مطلوبة")]
        public int BatchId { get; set; }
        public Batch Batch { get; set; }

        // 🔹 وقت البداية المجدول (HH:mm)
        public TimeSpan? ScheduledTime { get; set; }

        // 🔹 المدة المخططة بالدقائق
        public int? DurationMinutes { get; set; }

        // 🔹 ميعاد الانتهاء المتوقع (HH:mm) — يُحسب تلقائياً من ScheduledTime + DurationMinutes
        public TimeSpan? ScheduledEndTime { get; set; }

        // 🔹 وقت البدء الفعلي
        public DateTime? ActualStartTime { get; set; }
        public string? StartedByUserId { get; set; }
        public ApplicationUser? StartedByUser { get; set; }
        public string? StartedByRole { get; set; }

        // 🔹 وقت الانتهاء الفعلي
        public DateTime? ActualEndTime { get; set; }
        public string? EndedByUserId { get; set; }
        public ApplicationUser? EndedByUser { get; set; }
        public string? EndedByRole { get; set; }

        // 🔹 العلاقات
        public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
        public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    }
}
