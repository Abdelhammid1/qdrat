using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Users
{
    public class EditNameViewModel
    {
        [Required]
        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; }

        public DateTime? LastNameChangeDate { get; set; }

        public bool IsAllowedToChange => !LastNameChangeDate.HasValue || LastNameChangeDate.Value.AddDays(7) <= DateTime.Now;
    }

}
