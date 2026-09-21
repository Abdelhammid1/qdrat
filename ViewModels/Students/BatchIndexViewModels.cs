using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Students
{
    public class BatchIndexVm
    {
        public List<BatchCardVm> Batches { get; set; } = new();
        public int TotalStudents { get; set; }
        public int TotalBatches { get; set; }
        public int ActiveBatches { get; set; }
        public int TotalCourses { get; set; }
    }

    public class BatchCardVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string BranchName { get; set; } = "";
        public int StudentCount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public string GenderLabel { get; set; } = "";
    }

    public class BatchStudentsVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string BranchName { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<StudentInBatchVm> Students { get; set; } = new();
    }

    public class BatchBreakdownRowVm
    {
        public string BatchName { get; set; } = "";
        public string CourseName { get; set; } = "";
        public int TotalLec { get; set; }
        public int Present { get; set; }
        public int Absent { get; set; }
        public int TotalHw { get; set; }
        public int SubmittedHw { get; set; }
        public int TotalEx { get; set; }
        public int SubmittedEx { get; set; }
        public double AttPct { get; set; }
        public double HwPct { get; set; }
        public double ExPct { get; set; }
        public double Health { get; set; }
    }

    public class StudentInBatchVm
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? Gender { get; set; }
        public string? Level { get; set; }
        public string? EnrollmentStatus { get; set; }
        public DateTime EnrolledAt { get; set; }
        public int AttendedLectures { get; set; }
        public int TotalLectures { get; set; }
        public int SubmittedHomeworks { get; set; }
        public int TotalHomeworks { get; set; }
        public int SubmittedExams { get; set; }
        public int TotalExams { get; set; }
    }
}
