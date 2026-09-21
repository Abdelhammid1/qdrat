using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Users
{
    public class BulkUserImportRowVm
    {
        [Required] public string FullName { get; set; }
        public string? Email { get; set; }
        public string? UserName { get; set; }
        public string? NationalID { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Gender { get; set; }
        public string? School { get; set; }
        public string? Level { get; set; }
        public int? BranchId { get; set; }
        public int? ParentId { get; set; }
        public string? Password { get; set; }
    }
}
