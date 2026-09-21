using System;

namespace QdratNew.ViewModels.Partner.Reports
{
    public class StudentHomeworkDetailVM
    {
        public int HomeworkSetId { get; set; }
        public string HomeworkTitle { get; set; }

        public int StudentId { get; set; }
        public string StudentName { get; set; }

        public DateTime AssignedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public int AttemptCount { get; set; }
        public double? Score { get; set; }

        public bool IsLate { get; set; }
    }
}
