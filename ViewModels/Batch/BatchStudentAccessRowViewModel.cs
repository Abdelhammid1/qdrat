namespace QdratNew.ViewModels.Batch
{
    public class BatchStudentAccessRowViewModel
    {
        public int StudentId { get; set; }
        public string? UserId { get; set; }
        public string FullName { get; set; } = "";
        public string? NationalID { get; set; }
        public string? Phone { get; set; }
        public string? School { get; set; }
        public string? Level { get; set; }
        public string? EnrollmentStatus { get; set; }
        public DateTime EnrolledAt { get; set; }
        // true = الطالب لديه تسجيل في دفعة أخرى غير هذه الدفعة
        public bool HasActiveEnrollment { get; set; }
        public bool CanAccessStudentArea { get; set; }
        public DateTime? AccessRevokedAt { get; set; }
        public string? AccessRevokedByUserName { get; set; }
    }
}
