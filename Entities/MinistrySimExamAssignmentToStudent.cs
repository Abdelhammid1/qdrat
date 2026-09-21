using System;

namespace QdratNew.Entities
{
    // إسناد اختبار معمل القياس لطالب فردي
    // ملاحظة تصميمية (ADR-MSE-4): عمدًا لا يوجد حقل AssignmentTargetType Enum موحّد هنا —
    // اتّباع نفس نمط المشروع (كيانات منفصلة). إضافة MinistrySimExamAssignmentToGuest مستقبلاً
    // تكون كيانًا ثالثًا مستقلاً بنفس الشكل، دون أي تعديل على هذا الكيان أو على MinistrySimExamAssignmentToBatch.
    public class MinistrySimExamAssignmentToStudent
    {
        public int Id { get; set; }

        public int MinistrySimExamId { get; set; }
        public virtual MinistrySimExam MinistrySimExam { get; set; }

        public int StudentId { get; set; }
        public virtual Student Student { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.Now;
        public string SourceType { get; set; } = "Official"; // نفس اصطلاح ExamAssignmentToStudent.SourceType

        // Sprint 17 (MSE-J / J1): أونلاين (فتح مباشر) أو حضوري (يلزم رمزًا مرجعيًا من الأدمن في المعمل).
        public bool IsOnline { get; set; } = true;
        public string? ReferenceCode { get; set; }
    }
}
