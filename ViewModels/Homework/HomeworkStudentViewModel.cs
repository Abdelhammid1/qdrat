using QdratNew.Enums;

namespace QdratNew.ViewModels.Homework
{
    public class HomeworkStudentViewModel
    {
        public int HomeworkId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public bool IsSent { get; set; }
        public HomeworkStatus Status { get; set; }
        public string BehaviorFlag { get; set; } // Normal / Suspicious / CheatingLikely
        public double AvgTimePerQuestion { get; set; }
        public int FastAnswersCount { get; set; }
        public double? Score { get; set; }
        public int HomeworkCount { get; set; }
        public List<int> HomeworkIds { get; set; }
        public double RiskScore { get; internal set; }
        public StudentBehaviorLevel BehaviorLevel { get; internal set; }
        public DateTime AssignedAt { get; set; }
        public int HoursSinceAssigned { get; set; }
        public bool IsOverdue24Hours { get; set; }
        public bool IsHighRiskLateSubmission { get; set; }
        public bool StudentReminderContacted { get; set; }
        public DateTime? StudentReminderContactedAt { get; set; }
        public bool ParentContacted { get; set; }
        public DateTime? ParentContactedAt { get; set; }

        /// <summary>true = محاولة الواجب هذه موقوفة حاليًا بسبب رصد ترجمة المتصفح (Translation Guard)</summary>
        public bool IsIntegrityBlocked { get; set; }
    }
}
