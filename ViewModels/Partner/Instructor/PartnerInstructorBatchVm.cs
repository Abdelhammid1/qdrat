namespace QdratNew.ViewModels.Partner.Instructor
{
    public class PartnerInstructorBatchVm
    {
        public int BatchId { get; set; }

        public string BatchName { get; set; } = string.Empty;

        public int CurriculumId { get; set; }

        public string CurriculumTitle { get; set; } = string.Empty;

        // Optional – في حال احتجته لاحقاً للفلترة أو الروابط
        public int? BranchId { get; set; }

        public string? BranchName { get; set; }
    }

}
