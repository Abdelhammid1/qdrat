using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class ContactMessage
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string FullName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [StringLength(30)]
        public string? PhoneNumber { get; set; }

        [Required, StringLength(1000)]
        public string Message { get; set; }

        public string? IPAddress { get; set; }

        public bool IsRead { get; set; } = false; // ✅ لم يُقرأ بعد

        [StringLength(50)]
        public string Status { get; set; } = "جديدة"; // جديدة - تم التواصل - قيد المتابعة - مرفوضة

        [StringLength(500)]
        public string? AdminNotes { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUpdated { get; set; }
    }
}
