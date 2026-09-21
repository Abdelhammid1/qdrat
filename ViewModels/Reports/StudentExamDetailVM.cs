using System;

namespace QdratNew.ViewModels.Partner.Reports
{
    public class StudentExamDetailVM
    {
        public int ExamAssignmentId { get; set; }
        public string ExamTitle { get; set; }

        public int StudentId { get; set; }
        public string StudentName { get; set; }

        public DateTime AssignedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public int DurationMinutes { get; set; }
        public int? Score { get; set; }

        public string Status { get; set; }
    }
}
