
namespace QdratNew.ViewModels.Partner.Instructor
{
    public class PartnerInstructorListVm
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public string NationalID { get;  set; }
        public List<string> Curriculums { get;  set; }
    }

}
