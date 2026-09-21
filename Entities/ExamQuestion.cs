using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class ExamQuestion
    {
        // ====================================
        // 🔑 المفتاح الأساسي الجديد (Identity)
        // ====================================
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // ====================================
        // 🔹 السؤال
        // ====================================
        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        public int Order { get; set; }
        public bool IsManuallySelected { get; set; } = false;

        // ====================================
        // 🔹 ربط الاختبار الأساسي (اختياري)
        // ====================================
        public int? ExamId { get; set; }
        public Exam? Exam { get; set; }

        // ====================================
        // 🔹 ربط الاختبار المرسل للدفعة
        // ====================================
        public int? ExamAssignmentId { get; set; }

        [ForeignKey(nameof(ExamAssignmentId))]
        public ExamAssignmentToBatch? ExamAssignment { get; set; }

        // ====================================
        // 🔹 ربط الاختبار المرسل لطالب محدد (اختياري)
        // ====================================
        public int? ExamAssignmentToStudentId { get; set; }

        [ForeignKey(nameof(ExamAssignmentToStudentId))]
        public ExamAssignmentToStudent? StudentAssignment { get; set; }
    }
}
