namespace QdratNew.ViewModels.Users
{
    public class UsersIndexVm
    {
        public string Title { get; set; }
        public string FilterDescription { get; set; }
        public List<UserRowDto> Users { get; set; } = new();
    }
}
