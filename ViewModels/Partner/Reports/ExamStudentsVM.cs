using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Partner.Reports
{
    public class ExamStudentsVM
    {
        public int ExamAssignmentId { get; set; }
        public string ExamTitle { get; set; }

        public List<ExamStudentItemVM> Students { get; set; }
            = new();
    }

    public class ExamStudentItemVM
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }

        public bool HasStarted { get; set; }
        public bool IsSubmitted { get; set; }

        public DateTime? StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public int? Score { get; set; }
        public string Status { get; set; }
    }
}
