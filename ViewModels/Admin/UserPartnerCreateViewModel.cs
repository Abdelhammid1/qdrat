namespace QdratNew.ViewModels.Admin
{
    public class UserPartnerCreateViewModel
    {
        public string UserId { get; set; }

        // ✅ بدل PartnerId الواحد
        public List<int> PartnerIds { get; set; } = new();

        public List<UserItem> Users { get; set; } = new();
        public List<PartnerItem> Partners { get; set; } = new();

        public class UserItem
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        public class PartnerItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
