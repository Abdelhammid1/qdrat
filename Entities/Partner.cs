namespace QdratNew.Entities
{
    public class Partner
    {
        public int Id { get; set; }

        public string Name { get; set; }              // اسم المدرسة / الشريك
        public string Code { get; set; }              // كود فريد (للاستخدام الداخلي)

        public string? LogoPath { get; set; }         // لوجو الشريك

        public DateTime PartnershipStart { get; set; }
        public DateTime PartnershipEnd { get; set; }

        public bool IsActive =>
            DateTime.Now >= PartnershipStart &&
            DateTime.Now <= PartnershipEnd;

        public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    }

}
