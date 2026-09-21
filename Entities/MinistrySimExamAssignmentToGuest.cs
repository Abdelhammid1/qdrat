using System;

namespace QdratNew.Entities
{
    // إسناد اختبار معمل القياس لطالب ضيف/زائر — الكيان الثالث المخطط له في ADR-MSE-4
    // (راجع الملاحظة في MinistrySimExamAssignmentToStudent/ToBatch)، بنفس شكل الكيانين الآخرين ومنفصل عنهما تمامًا.
    public class MinistrySimExamAssignmentToGuest
    {
        public int Id { get; set; }

        public int MinistrySimExamId { get; set; }
        public virtual MinistrySimExam MinistrySimExam { get; set; }

        public int GuestStudentId { get; set; }
        public virtual MinistrySimExamGuestStudent GuestStudent { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.Now;
    }
}
