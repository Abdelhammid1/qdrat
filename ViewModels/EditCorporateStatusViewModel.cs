using QdratNew.Enums;

namespace QdratNew.ViewModels
{
    public class EditCorporateStatusViewModel
    {
        public int Id { get; set; }

        // 🔹 بيانات المرسل (للعرض فقط — غير قابلة للتعديل من هذه الشاشة)
        public string FullName { get; set; }
        public string EmployeeNumber { get; set; }
        public string Email { get; set; }
        public string NationalID { get; set; }
        public string PhoneNumber { get; set; }
        public string Residence { get; set; }
        public string CompanyName { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string? HandledBy { get; set; }

        // 🔹 بيانات النموذج القابلة للتعديل
        public RegistrationStatus CurrentStatus { get; set; }
        public string? AdminNotes { get; set; }
    }
}
