namespace QdratNew.Entities
{
    public class UserPartner
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public int PartnerId { get; set; }
        public Partner Partner { get; set; }
    }
}
