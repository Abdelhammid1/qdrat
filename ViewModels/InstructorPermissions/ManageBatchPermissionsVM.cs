using QdratNew.Entities;

namespace QdratNew.ViewModels.InstructorPermissions
{
    public class ManageBatchPermissionsVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseName { get; set; } = "";
        public bool IsArchived { get; set; }

        public List<BatchInstructorRowVM> Instructors { get; set; } = new();
        public List<BatchEmployeeRowVM> Employees { get; set; } = new();
    }

    public class BatchInstructorRowVM
    {
        public int InstructorId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsActive { get; set; }
        public List<FeaturePermissionVM> FeaturePermissions { get; set; } = new();

        public bool HasAnyPermission => FeaturePermissions.Any(f => f.IsGranted);
        public bool HasAllPermissions => FeaturePermissions.All(f => f.IsGranted);
    }

    public class BatchEmployeeRowVM
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string RoleName { get; set; } = "";
        public List<FeaturePermissionVM> FeaturePermissions { get; set; } = new();

        public bool HasAnyPermission => FeaturePermissions.Any(f => f.IsGranted);
        public bool HasAllPermissions => FeaturePermissions.All(f => f.IsGranted);
    }
}
