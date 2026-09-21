using QdratNew.Enums;

namespace QdratNew.Entities
{
    // اختيار الأدمن لكل مؤشر (Lesson) داخل محور بمرحلة معيّنة
    public class MinistrySimExamStageIndicatorSelection
    {
        public int Id { get; set; }

        public int MinistrySimExamStageId { get; set; }
        public virtual MinistrySimExamStage MinistrySimExamStage { get; set; }

        public int SectionId { get; set; } // المحور (كمي أو لفظي حسب المرحلة)
        public virtual Section Section { get; set; }

        public int LessonId { get; set; } // المؤشر
        public virtual Lesson Lesson { get; set; }

        public DifficultyLevel Difficulty { get; set; }
        public int RequestedCount { get; set; } // العدد الذي اختاره الأدمن لهذا المؤشر بهذه الصعوبة
    }
}
