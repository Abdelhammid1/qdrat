namespace QdratNew.ViewModels.Partner
{
    public class PartnerBatchDetailsViewModel
    {
        public int BatchId { get; set; } // ✅ أضف هذا

        public string BatchName { get; set; }
        public string CourseName { get; set; }
        public int StudentsCount { get; set; }

        // 🟢 قائمة طلاب الدفعة
        public List<PartnerBatchStudentItemViewModel> Students { get; set; }
            = new();
    }

    public class PartnerBatchStudentItemViewModel
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public string NationalId { get; set; }
        public string Phone { get; set; }
        public string Level { get; set; }
    }




}
