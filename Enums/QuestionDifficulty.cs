using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    public enum QuestionDifficulty
    {
        [Display(Name = "سهل")]
        Easy = 0,

        [Display(Name = "متوسط")]
        Medium = 1,

        [Display(Name = "صعب")]
        Hard = 2
    }
}
