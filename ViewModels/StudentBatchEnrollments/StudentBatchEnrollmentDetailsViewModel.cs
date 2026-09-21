using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.StudentBatchEnrollments
{
    public class StudentBatchEnrollmentDetailsViewModel
    {
        public int Id { get; set; }

        public int StudentID { get; set; }

        public int BatchId { get; set; }

        public string StudentName { get; set; } = string.Empty;

        public string BatchName { get; set; } = string.Empty;

        public DateTime EnrolledAt { get; set; }

        public string Status { get; set; } = string.Empty;

        public List<StudentBatchEnrollmentRelatedBatchVm> RelatedBatches { get; set; } = new List<StudentBatchEnrollmentRelatedBatchVm>();
    }

    public class StudentBatchEnrollmentRelatedBatchVm
    {
        public int EnrollmentId { get; set; }

        public int StudentID { get; set; }

        public int BatchId { get; set; }

        public string StudentName { get; set; } = string.Empty;

        public string BatchName { get; set; } = string.Empty;

        public DateTime EnrolledAt { get; set; }

        public string Status { get; set; } = string.Empty;

        public bool IsCurrent { get; set; }
    }
}