using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Admin
{
    public class AdminUserProfileAssignVM
    {
        [Required]
        public string UserId { get; set; }

        public int? AdminProfileId { get; set; }

        public List<SelectListItem> Users    { get; set; } = new();
        public List<SelectListItem> Profiles { get; set; } = new();

        // وضع التعديل — يُملأ من الكنترولر عند تمرير userId
        public bool   IsEditMode  { get; set; }
        public string EditUserName  { get; set; } = "";
        public string EditUserEmail { get; set; } = "";
        public string EditCurrentProfile { get; set; } = "";
    }
}
