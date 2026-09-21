using System;
using System.Collections.Generic;

namespace QdratNew.Entities
{
    // محاولة الطالب — محاولة واحدة فقط لكل (MinistrySimExamId, StudentId)
    public class MinistrySimExamStudentAttempt
    {
        public int Id { get; set; }

        public int MinistrySimExamId { get; set; }
        public virtual MinistrySimExam MinistrySimExam { get; set; }

        public int StudentId { get; set; }
        public virtual Student Student { get; set; }

        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsCompleted { get; set; } = false;
        public double? TotalScorePercent { get; set; }

        // Sprint 19 (MSE-K / K3): ملاحظات/توصيات المدرّس تُكتب قبل طباعة تقرير ولي الأمر لهذه المحاولة تحديدًا
        public string? RecommendationsNote { get; set; }

        public virtual ICollection<MinistrySimExamStudentStageProgress> StageProgress { get; set; } = new List<MinistrySimExamStudentStageProgress>();
        public virtual ICollection<MinistrySimExamStudentAnswer> Answers { get; set; } = new List<MinistrySimExamStudentAnswer>();
    }
}
