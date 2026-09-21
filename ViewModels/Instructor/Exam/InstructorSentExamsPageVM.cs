using QdratNew.ViewModels.Exam;

namespace QdratNew.ViewModels.Instructor.Exam
{
    public class InstructorSentExamsPageVM
    {
        public List<InstructorSentExamItemVM> BatchExams { get; set; } = new();

        public List<InstructorSentExamItemVM> IndividualExams { get; set; } = new();

        public List<InstructorSentExamItemVM> PlacementExams { get; set; } = new();
        public List<BatchDropdownVM> Batches { get; set; } = new();

        public List<InstructorSentExamItemVM> PerformanceExams { get; set; } = new();
    }

    public class BatchDropdownVM
    {
        public int BatchId { get; set; }
        public string Name { get; set; }
    }
}
