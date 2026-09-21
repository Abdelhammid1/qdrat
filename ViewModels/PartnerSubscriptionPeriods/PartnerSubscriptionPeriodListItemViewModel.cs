using System;

namespace QdratNew.ViewModels.PartnerSubscriptionPeriods
{
    public class PartnerSubscriptionPeriodListItemViewModel
    {
        public int Id { get; set; }

        // =========================
        // 🔹 المدة
        // =========================
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // =========================
        // 🔹 الطلاب
        // =========================
        public int? MaxStudents { get; set; }
        public int UsedStudentsCount { get; set; }

        // =========================
        // 🔹 الحالة (محسوبة في الكنترولر)
        // =========================
        public string Status { get; set; } = string.Empty;

        // =========================
        // 🔹 خصائص عرض مساعدة
        // =========================
        public bool IsActive => Status == "نشط";
        public bool IsExpired => Status == "منتهي";
        public bool IsUpcoming => Status == "مستقبلي";
    }
}
