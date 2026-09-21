namespace QdratNew.ViewModels.Instructor.Exam
{
    public class SentExamsPageVM
    {
        public List<SentExamListVM> BatchExams { get; set; } = new();
        public List<SentExamListVM> IndividualExams { get; set; } = new();
        public List<SentExamListVM> PlacementExams { get; set; } = new();
        public List<SentExamListVM> PerformanceExams { get; set; } = new();
    }
}
