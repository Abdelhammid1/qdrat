namespace QdratNew.ViewModels.Instructor.InstructorBatches

{
    public class InstructorBatchesIndexViewModel
    {
        public List<InstructorBatchListItemViewModel> Batches { get; set; } = new();
    }

    public class InstructorBatchListItemViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public int StudentsCount { get; set; }
        public string AccessSource { get; set; } = string.Empty;
    }

    public class InstructorBatchDetailsViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public int StudentsCount { get; set; }

        public List<InstructorBatchStudentViewModel> Students { get; set; } = new();
        public List<InstructorBatchCurriculumViewModel> Curriculums { get; set; } = new();
    }

    public class InstructorBatchStudentViewModel
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Level { get; set; }
        public string EnrollmentStatus { get; set; } = string.Empty;
    }

    public class InstructorBatchCurriculumViewModel
    {
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; } = string.Empty;
    }
}