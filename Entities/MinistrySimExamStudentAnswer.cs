using System;

namespace QdratNew.Entities
{
    public class MinistrySimExamStudentAnswer
    {
        public int Id { get; set; }

        public int MinistrySimExamStudentAttemptId { get; set; }
        public virtual MinistrySimExamStudentAttempt Attempt { get; set; }

        public int MinistrySimExamStageQuestionId { get; set; }
        public virtual MinistrySimExamStageQuestion StageQuestion { get; set; }

        public int? SelectedOptionId { get; set; }
        public bool? IsCorrect { get; set; }
        public DateTime AnsweredAt { get; set; } = DateTime.Now;
        public bool IsFlaggedForReview { get; set; } = false;
    }
}
