using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Admin
{
    public class AdminProfileEditVM
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم البروفايل مطلوب")]
        [MaxLength(150)]
        public string Name { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }
    }
}
