using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Admin
{
    public class AssignPartnerViewModel
    {

        public int? SelectedPartnerId { get; set; }
        public string PartnerName { get; set; }

        public string SelectedUserId { get; set; }
        public List<SelectListItem> Users { get; set; } = new();

        // ✅ الجديد
        public List<AssignedPartnerUserViewModel> AssignedUsers { get; set; } = new();
        public string Email { get; set; }

              public List<SelectListItem> Partners { get; set; } = new();


    }
}
