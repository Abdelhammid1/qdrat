using System.Collections.Generic;

namespace QdratNew.Entities
{
    // مرحلة واحدة من 5 (محور كمي + محور لفظي) — StageNumber فريد لكل MinistrySimExamId
    public class MinistrySimExamStage
    {
        public int Id { get; set; }

        public int MinistrySimExamId { get; set; }
        public virtual MinistrySimExam MinistrySimExam { get; set; }

        public int StageNumber { get; set; } // 1..5

        public int QuantSectionId { get; set; } // المحور الكمي
        public virtual Section QuantSection { get; set; }
        public int QuantQuestionCount { get; set; }

        public int VerbalSectionId { get; set; } // المحور اللفظي
        public virtual Section VerbalSection { get; set; }
        public int VerbalQuestionCount { get; set; }

        public int DurationMinutes { get; set; } = 26;

        public virtual ICollection<MinistrySimExamStageIndicatorSelection> IndicatorSelections { get; set; } = new List<MinistrySimExamStageIndicatorSelection>();
        public virtual ICollection<MinistrySimExamStageQuestion> Questions { get; set; } = new List<MinistrySimExamStageQuestion>();
    }
}
