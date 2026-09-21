namespace QdratNew.ViewModels.Partner
{
    public class PartnerStudentReportViewModel
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public string NationalId { get; set; }
        public string Phone { get; set; }
        public string Level { get; set; }
        public string School { get; set; }

        public List<string> Batches { get; set; } = new();
    }
}
