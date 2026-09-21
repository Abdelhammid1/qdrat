namespace QdratNew.ViewModels.Admin
{
    public class PartnerStudentListViewModel
    {
        public int StudentId { get; set; }
        // 🔹 جديد (مفتاح الإجراءات)
        public string? UserId { get; set; }
        public string NationalID { get; set; }   // ✅ الجديد

        public string FullName { get; set; }
        public string Phone { get; set; }
        public string School { get; set; }
        public string Level { get; set; }
    }

}
