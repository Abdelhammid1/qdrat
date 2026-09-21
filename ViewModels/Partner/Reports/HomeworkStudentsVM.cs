using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Partner.Reports
{
    public class HomeworkStudentsVM
    {
        public int HomeworkSetId { get; set; }
        public string HomeworkTitle { get; set; }

        public List<HomeworkStudentItemVM> Students { get; set; }
            = new();
    }

    public class HomeworkStudentItemVM
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }

        public bool IsSubmitted { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public bool IsLate { get; set; }
        public double? Score { get; set; }
    }
}
