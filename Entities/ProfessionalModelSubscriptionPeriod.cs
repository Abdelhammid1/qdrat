namespace QdratNew.Entities
{
    public class ProfessionalModelSubscriptionPeriod
    {
        public int Id { get; set; }

        public int ProfessionalModelId { get; set; }
        public ProfessionalModel ProfessionalModel { get; set; }

        public int PartnerSubscriptionPeriodId { get; set; }
        public PartnerSubscriptionPeriod PartnerSubscriptionPeriod { get; set; }

    }
}
