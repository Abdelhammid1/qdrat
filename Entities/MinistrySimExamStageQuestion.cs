using System;

namespace QdratNew.Entities
{
    // ربط السؤال بالمرحلة + الترقيم التراكمي (StageOrder داخل المرحلة، GlobalOrder عبر كل المراحل)
    public class MinistrySimExamStageQuestion
    {
        public int Id { get; set; }

        public int MinistrySimExamStageId { get; set; }
        public virtual MinistrySimExamStage MinistrySimExamStage { get; set; }

        public Guid QuestionId { get; set; }
        public virtual Question Question { get; set; }

        public int StageOrder { get; set; }  // 1..24
        public int GlobalOrder { get; set; }  // 1..120
        public bool IsQuant { get; set; }     // true = كمي، false = لفظي
        public bool IsManuallySelected { get; set; } = false; // true بعد استبدال/إضافة يدوي من الأدمن
    }
}
