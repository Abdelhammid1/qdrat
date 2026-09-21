using System;

namespace QdratNew.Entities
{
    public class PromoLead
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public PromoExamSession Session { get; set; }

        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string Status { get; set; } = "جديد"; // جديد - تم التواصل - مهتم - انضم
        public DateTime RequestedAt { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; internal set; }
    }
}
