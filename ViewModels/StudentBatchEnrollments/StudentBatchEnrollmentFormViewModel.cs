using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.StudentBatchEnrollments
{
    public class StudentBatchEnrollmentFormViewModel
    {
        public int Id { get; set; }
        public int StudentID { get; set; }
        public int BatchId { get; set; }
        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Active";

        public List<SelectListItem> Students { get; set; } = new();
        public List<SelectListItem> Batches { get; set; } = new();
        public List<int> SelectedBatchIds { get; set; } = new();

    }
}
