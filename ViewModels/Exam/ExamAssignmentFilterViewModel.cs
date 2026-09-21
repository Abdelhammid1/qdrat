using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Exam
{
    public class ExamAssignmentFilterViewModel
    {
        public int? BatchId { get; set; }
        public int? CurriculumId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool ShowArchived { get; set; }

        // قائمة النتائج
        public List<ExamAssignmentRowViewModel> Results { get; set; }
        public List<ExamBatchCardVM> BatchCards { get; set; } = new();

        // للاختيارات في الفلاتر
        public List<SelectListItem> Batches { get; set; }
        public List<SelectListItem> Curriculums { get; set; }
        public int TotalBatches { get; set; }
        public int TotalExamsCount { get; set; }
        public int TotalSentExams { get; set; }
        public int TotalAssignedStudents { get; set; }
        public int TotalCompletedStudents { get; set; }
    }

    public class ExamAssignmentRowViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string BatchName { get; set; }
        public string CurriculumTitle { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsSent { get; set; }
        public int StudentCount { get; set; }
        public bool IsArchived { get; set; }
    }
}
