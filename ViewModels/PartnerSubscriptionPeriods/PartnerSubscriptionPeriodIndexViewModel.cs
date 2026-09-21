namespace QdratNew.ViewModels.PartnerSubscriptionPeriods
{
    public class PartnerSubscriptionPeriodIndexViewModel
    {
        // =========================
        // 🔹 بيانات العقد
        // =========================
        public int SubscriptionId { get; set; }

        public string PartnerName { get; set; } = string.Empty;

        // =========================
        // 🔹 فترات العقد
        // =========================
        public List<PartnerSubscriptionPeriodListItemViewModel> Periods { get; set; }
            = new();
    }

}
