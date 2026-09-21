using QdratNew.Entities;

namespace QdratNew.ViewModels.InstructorPermissions
{
    public class ManageInstructorPermissionsVM
    {
        public int InstructorId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsActive { get; set; }
        public List<InstructorBatchPermissionRowVM> BatchRows { get; set; } = new();
    }

    public class InstructorBatchPermissionRowVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseName { get; set; } = "";
        public bool IsArchived { get; set; }
        public List<FeaturePermissionVM> FeaturePermissions { get; set; } = new();

        public bool HasAnyPermission => FeaturePermissions.Any(f => f.IsGranted);
        public bool HasAllPermissions => FeaturePermissions.All(f => f.IsGranted);
    }

    public class FeaturePermissionVM
    {
        public InstructorBatchFeature Feature { get; set; }
        public string FeatureNameAr { get; set; } = "";
        public bool IsGranted { get; set; }
        public DateTime? GrantedAt { get; set; }
    }
}
