using QdratNew.Enums;

namespace QdratNew.ViewModels
{
    public class EditCorporateStatusViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public RegistrationStatus CurrentStatus { get; set; }
        public string? AdminNotes { get; set; }
    }
}
