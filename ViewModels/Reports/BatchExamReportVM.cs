using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Partner.Reports
{
    public class BatchExamReportVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }

        public List<BatchExamItemVM> Exams { get; set; }
            = new();
    }

    public class BatchExamItemVM
    {
        public int ExamAssignmentId { get; set; }
        public string Title { get; set; }

        public DateTime AssignedAt { get; set; }

        public int TotalStudents { get; set; }
        public int StartedCount { get; set; }
        public int SubmittedCount { get; set; }

        public double AverageScore { get; set; }
    }
}
