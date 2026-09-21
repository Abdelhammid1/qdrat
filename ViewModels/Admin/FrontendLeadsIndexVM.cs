using System.Collections.Generic;

namespace QdratNew.ViewModels.Admin
{
    public class FrontendLeadsIndexVM
    {
        public int TotalCount { get; set; }
        public int ContactedCount { get; set; }
        public int NotContactedCount { get; set; }

        public string CurrentFilter { get; set; } = "all";

        public List<FrontendLeadAdminVM> Leads { get; set; } = new();
    }
}
