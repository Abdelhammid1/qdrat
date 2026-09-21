namespace QdratNew.ViewModels.Admin
{
    public class UserPartnerEditViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }

        public List<int> SelectedPartnerIds { get; set; } = new();

        public List<PartnerItem> AllPartners { get; set; } = new();

        public class PartnerItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
