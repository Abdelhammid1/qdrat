namespace QdratNew.ViewModels.Admin
{
    public class UserPartnerDetailsViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }

        public List<PartnerItem> Partners { get; set; } = new();

        public class PartnerItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string? LogoPath { get; set; }
        }
    }
}
