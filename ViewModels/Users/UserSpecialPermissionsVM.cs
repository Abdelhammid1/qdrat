namespace QdratNew.ViewModels.Users
{
    public class UserSpecialPermissionsVM
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string? UserRole { get; set; }

        // الصلاحيات المتخصصة الحالية
        public bool CanImpersonate { get; set; }
    }
}
