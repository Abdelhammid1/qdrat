namespace QdratNew.ViewModels.InstructorPermissions
{
    public class InstructorPermissionSummaryVM
    {
        public int InstructorId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsActive { get; set; }
        public int GrantedBatchesCount { get; set; }
        public int TotalPermissionsCount { get; set; }
    }
}
