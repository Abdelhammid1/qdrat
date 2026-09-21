using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels
{
    public class RejectSessionViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يرجى كتابة سبب الرفض")]
        public string RejectionReason { get; set; }
    }
}
