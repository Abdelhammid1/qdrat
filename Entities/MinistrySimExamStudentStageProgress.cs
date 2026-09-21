using System;

namespace QdratNew.Entities
{
    // آلية القفل الفعلية — بمجرد IsLocked = true لا يوجد أي مسار كود Backend يعيدها false
    public class MinistrySimExamStudentStageProgress
    {
        public int Id { get; set; }

        public int MinistrySimExamStudentAttemptId { get; set; }
        public virtual MinistrySimExamStudentAttempt Attempt { get; set; }

        public int StageNumber { get; set; } // 1..5

        public DateTime? StartedAt { get; set; }
        public DateTime? EndAt { get; set; }

        public bool IsLocked { get; set; } = false; // true = نهائي، لا رجوع
        public bool TimeExpired { get; set; } = false; // true إذا كان القفل بسبب انتهاء الوقت

        public double? StagePercentScore { get; set; }
    }
}
