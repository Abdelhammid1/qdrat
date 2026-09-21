namespace QdratNew.ViewModels.Admin
{
    public class AdminUserProfileIndexVM
    {
        public int Id { get; set; }
        public int ProfileId { get; set; }
        public string UserId { get; set; }   // ✅ مهم

        public string UserName { get; set; }
        public string ProfileName { get; set; }
        public DateTime AssignedAt { get; set; }
    }

}
