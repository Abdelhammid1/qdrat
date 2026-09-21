namespace QdratNew.ViewModels.Users
{
    public class UserListViewModel
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public List<string> Roles { get; set; } = new();
        public bool IsActive { get; set; }

    }
}
