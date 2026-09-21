using System.Collections.Generic;

namespace QdratNew.ViewModels.PartnerSubscriptions
{
    public class PartnerSubscriptionIndexViewModel
    {
        public int PartnerId { get; set; }
        public string PartnerName { get; set; } = string.Empty;

        public List<PartnerSubscriptionListItemViewModel> Subscriptions { get; set; }
            = new();

        public bool HasSubscriptions => Subscriptions.Any();
    }
}
