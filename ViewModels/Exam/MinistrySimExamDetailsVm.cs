using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    // Sprint 5 (MSE-C / C4): شاشة حالة المسودة — تسمح للأدمن بمعرفة مدى اكتمال الاختبار قبل النشر والعودة لاحقًا لإكماله
    public class MinistrySimExamDetailsVm
    {
        public int ExamId { get; set; }
        public string Title { get; set; }
        public string CourseName { get; set; }
        public bool IsPublished { get; set; }
        public DateTime? PublishedAt { get; set; }

        public List<MinistrySimExamStageStatusVm> Stages { get; set; } = new();

        // نتيجة ValidateExactCountsAsync (Sprint 5 / C3) — تُعرض للأدمن دون أن تمنع الحفظ كمسودة
        public bool IsReadyToPublish { get; set; }
        public List<string> ShortfallMessages { get; set; } = new();

        // Sprint 8 (MSE-E): ملخص الإسناد — يُعرض فقط بعد النشر (الإسناد يتطلب IsPublished == true)
        public int AssignedBatchesCount { get; set; }
        public int AssignedStudentsCount { get; set; }

        // Sprint 15 (MSE-I / I2): الدفعات المُسنَد لها الاختبار — تُستخدم لروابط "تحليلات الدفعة" لكل دفعة
        public List<MinistrySimExamAssignedBatchVm> AssignedBatches { get; set; } = new();
    }

    public class MinistrySimExamAssignedBatchVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
    }

    public class MinistrySimExamStageStatusVm
    {
        public int StageId { get; set; }
        public int StageNumber { get; set; }

        public string QuantSectionTitle { get; set; }
        public int QuantTarget { get; set; }
        public int QuantActual { get; set; }

        public string VerbalSectionTitle { get; set; }
        public int VerbalTarget { get; set; }
        public int VerbalActual { get; set; }

        public int DurationMinutes { get; set; }

        public bool IsComplete => QuantActual == QuantTarget && VerbalActual == VerbalTarget;
    }
}
