using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudyPlan
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Student")]
        public int StudentID { get; set; }
        public required virtual Student Student { get; set; }  // ✅ تم إضافة `virtual` لدعم `Lazy Loading`

        [Required]
        public required string Subject { get; set; }  // ✅ المادة الدراسية المتعلقة بالخطة

        [Required]
        public required string Title { get; set; }  // ✅ عنوان الخطة الدراسية

        public required string Description { get; set; }  // ✅ وصف للخطة الدراسية

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int HoursPerWeek { get; set; }  // ✅ عدد الساعات المخصصة أسبوعيًا

        public double CompletionPercentage { get; set; } = 0;  // ✅ نسبة التقدم في الخطة
        public bool IsCompleted { get; set; } = false;  // ✅ حالة الخطة (مكتملة أم لا)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // ✅ تحديد وقت إنشاء الخطة
    }
}
