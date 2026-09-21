using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Admin
{
    public class AdminUserProfileCreateVM
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        public int AdminProfileId { get; set; }

        public List<SelectListItem> Users { get; set; } = new();
        public List<SelectListItem> Profiles { get; set; } = new();
    }
}
