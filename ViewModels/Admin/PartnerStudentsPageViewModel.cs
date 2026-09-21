using System.Collections.Generic;

namespace QdratNew.ViewModels.Admin
{
    public class PartnerStudentsPageViewModel
    {
        public int PartnerId { get; set; }
        public string PartnerName { get; set; }
        public List<PartnerStudentListViewModel> Students { get; set; } = new();
    }

}
