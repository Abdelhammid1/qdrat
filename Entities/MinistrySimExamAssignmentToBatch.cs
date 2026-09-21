using System;

namespace QdratNew.Entities
{
    // إسناد اختبار معمل القياس لدفعة كاملة
    // ملاحظة تصميمية (ADR-MSE-4): كيان منفصل عمدًا عن MinistrySimExamAssignmentToStudent (بلا Discriminator موحّد)،
    // اتساقًا مع نمط المشروع القائم فعليًا (ExamAssignmentToBatch / ExamAssignmentToStudent). إضافة كيان "ضيف"
    // مستقبلاً تكون جدولاً ثالثًا مستقلاً بنفس الشكل، دون أي تعديل على هذا الكيان أو على MinistrySimExamAssignmentToStudent.
    public class MinistrySimExamAssignmentToBatch
    {
        public int Id { get; set; }

        public int MinistrySimExamId { get; set; }
        public virtual MinistrySimExam MinistrySimExam { get; set; }

        public int BatchId { get; set; }
        public virtual Batch Batch { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.Now;
        public bool IsSentToStudents { get; set; } = false;

        public int? CreatedByInstructorId { get; set; }
        public virtual Instructor CreatedByInstructor { get; set; }

        // Sprint 17 (MSE-J / J1): أونلاين (فتح مباشر) أو حضوري (يلزم رمزًا مرجعيًا من الأدمن في المعمل).
        // رمز واحد لكل سجل إسناد (= جلسة معمل واحدة تُعلَن شفهيًا لكل الحاضرين) وليس لكل طالب فرديًا.
        public bool IsOnline { get; set; } = true;
        public string? ReferenceCode { get; set; }
    }
}
