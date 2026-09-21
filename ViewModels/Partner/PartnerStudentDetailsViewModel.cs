namespace QdratNew.ViewModels.Partner
{
    public class PartnerStudentDetailsViewModel
    {
        public string FullName { get; set; }
        public string NationalId { get; set; }
        public string Phone { get; set; }

        public string Gender { get; set; }
        public string Level { get; set; }
        public string School { get; set; }

        public bool IsActiveForLearning { get; set; }

        public List<StudentEnrollmentVM> Enrollments { get; set; }
            = new();
        public int StudentId { get; internal set; }
        public List<PartnerStudentBatchVM> Batches { get; set; } = new();
    }
}
