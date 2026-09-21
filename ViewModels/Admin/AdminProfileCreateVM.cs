using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Admin
{
    public class AdminProfileCreateVM
    {
        [Required(ErrorMessage = "اسم البروفايل مطلوب")]
        [MaxLength(150)]
        [Display(Name = "اسم البروفايل")]
        public string Name { get; set; }

        [MaxLength(500)]
        [Display(Name = "الوصف")]
        public string Description { get; set; }
    }
}
