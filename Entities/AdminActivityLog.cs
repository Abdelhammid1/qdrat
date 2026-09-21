using System;

namespace QdratNew.Entities
{
    public class AdminActivityLog
    {
        public int Id { get; set; }

        public string AdminId { get; set; } = string.Empty; // FK to ApplicationUser
        public string AdminName { get; set; } = string.Empty;

        public string ActionType { get; set; } = string.Empty; // مثل: "إرسال واجب", "إعادة إرسال لطالب"
        public string Description { get; set; } = string.Empty;

        public int? HomeworkSetId { get; set; }
        public int? StudentId { get; set; }
        public int? BatchId { get; set; }

        // Sprint 3 (EC1) — سياق تعديلات أسئلة الاختبار: من/ليه/الأثر
        public int? ExamAssignmentId { get; set; }
        public int? ExamAssignmentToStudentId { get; set; }
        public string? Reason { get; set; }        // سبب التعديل كما كتبه الأدمن
        public string? ImpactJson { get; set; }    // ملخّص الأثر: JSON خفيف، مش نص حر

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
