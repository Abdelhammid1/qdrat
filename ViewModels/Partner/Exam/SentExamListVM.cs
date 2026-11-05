using System;

namespace QdratNew.ViewModels.Partner.Exam
{
    public class SentExamListVM
    {
        public int ExamId { get; set; }
        public int ExamAssignmentId { get; set; }
        public int BatchId { get; set; }

        public string ExamTitle { get; set; }
        public string BatchName { get; set; }

        public int StudentsCount { get; set; }

        // ✅ إضافات جديدة
        public int AttemptedCount { get; set; }
        public int NotAttemptedCount { get; set; }

        public DateTime SentAt { get; set; }

        // PPM-S5.3: أُرسل من نموذج احترافي
        public bool IsFromModel { get; set; }

        public string? SourceModelTitle { get; set; }

        // APD-S2.3: أرسله المعهد باسم الشريك
        public bool IsFromInstitute { get; set; }

        // APD-S2.3: يملكه الشريك فيعدّل عنوانه ومواعيده
        public bool CanEdit { get; set; }

        // APD-S2.1: الرقم المرجعي لاختبار حضوري يملكه الشريك ويعطيه لطلابه
        public string? ReferenceCode { get; set; }

    }
}
