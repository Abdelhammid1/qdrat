using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Partner.Reports
{
    public class BatchHomeworkReportVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }

        public List<BatchHomeworkItemVM> Homeworks { get; set; }
            = new();
    }

    public class BatchHomeworkItemVM
    {
        public int HomeworkSetId { get; set; }
        public string Title { get; set; }

        public DateTime SentAt { get; set; }

        public int TotalStudents { get; set; }
        public int SubmittedCount { get; set; }
        public int LateCount { get; set; }

        public double AverageScore { get; set; }
    }
}
