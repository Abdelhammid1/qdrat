namespace QdratNew.ViewModels.Batch
{
    public class BatchStudentsViewModel
    {
        
        public int BatchId { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string BatchName { get; set; }
        public string CourseName { get; set; }
        public string NationalID { get; set; }
        public string Level { get; set; }

        public List<BatchStudentVm> Students { get; set; } = new();
    }

    public class BatchStudentVm
    {
        public int StudentId { get; set; }
        public string NationalID { get; set; }
        public object NationalId { get; internal set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Level { get; set; }
        public string Gender { get; set; }
        public DateTime EnrolledAt { get; set; }
        public string Status { get; set; }
        public string? Stage { get; set; }

    }

}
