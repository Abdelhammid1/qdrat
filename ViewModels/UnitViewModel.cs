using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels
{
    public class UnitViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يرجى إدخال اسم الوحدة")]
        [Display(Name = "اسم الوحدة")]
        public string Title { get; set; }
    }
}
