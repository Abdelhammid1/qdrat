namespace QdratNew.ViewModels.Admin
{
    public class UserPartnerListItemViewModel
    {
        public int Id { get; set; }              // UserPartner.Id
        public string UserId { get; set; }        // AspNetUsers.Id
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string PartnerName { get; set; }
    }
}
