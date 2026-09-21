namespace QdratNew.ViewModels.StudentBatchEnrollments
{
    public class StudentBatchEnrollmentListViewModel
    {
        public int Id { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; }
        public DateTime EnrolledAt { get; set; }
        public string Status { get; set; }
    }
}
