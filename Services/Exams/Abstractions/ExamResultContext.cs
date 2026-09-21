using QdratNew.Enums.Abstractions;

namespace QdratNew.Services.Exams.Abstractions
{
    public class ExamResultContext
    {
        public int StudentId { get; set; }

        // General Exam (Batch)
        public int? ExamAssignmentId { get; set; }

        // Individual / Placement
        public int? ExamAssignmentToStudentId { get; set; }
        public int? PlacementExamId { get; set; }

        // Performance Indicator
        public int? PerformanceIndicatorExamId { get; set; }

        public ExamKind Kind { get; set; }
        public int ExamId { get; set; }

        // ================================
        // 🟢 Helper – لا يؤثر على أي كود حالي
        // ================================
        public bool IsIndividual =>
            ExamAssignmentId == null &&
            ExamAssignmentToStudentId.HasValue;

        public int EffectiveAssignmentId =>
            IsIndividual
                ? ExamAssignmentToStudentId!.Value
                : ExamAssignmentId!.Value;
    }
}
