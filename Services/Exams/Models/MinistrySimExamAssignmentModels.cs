using System.Collections.Generic;

namespace QdratNew.Services.Exams.Models
{
    // Sprint 8 (MSE-E): نتيجة إسناد اختبار معمل القياس لدفعة/دفعات أو لطالب/طلاب محددين
    public class MinistrySimExamAssignmentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int AssignedBatchesCount { get; set; }
        public int AssignedStudentsCount { get; set; }
        // رسائل توضيحية لكل طالب/دفعة تم تجاوزه (مُسنَد بالفعل، أو لديه محاولة مسجَّلة أصلاً — القرار #7 / E4)
        public List<string> SkippedMessages { get; set; } = new();

        // Sprint 17 (MSE-J / J2): الرمز المرجعي المولَّد لهذه العملية — يُعرض للأدمن فقط عند إسناد حضوري (IsOnline == false)
        public string? GeneratedReferenceCode { get; set; }
    }
}
