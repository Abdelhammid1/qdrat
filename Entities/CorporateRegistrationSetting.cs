namespace QdratNew.Entities
{
    public class CorporateRegistrationSetting
    {
        public int Id { get; set; }

        public string CompanyName { get; set; }  // مثل: petrojet

        public bool IsClosed { get; set; } = false;

        public DateTime? ClosedAt { get; set; }
        public DateTime? ReopenedAt { get; set; }
        public DateTime? AutoCloseAt { get; set; }

        public bool IsVisibleOnHomePage { get; set; } = true;

    }

}
