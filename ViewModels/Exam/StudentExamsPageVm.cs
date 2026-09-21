namespace QdratNew.ViewModels.Exam
{
    public class StudentExamsPageVm
    {
        public List<ExamListVm> BatchExams { get; set; } = new();
        public List<ExamListVm> IndividualExams { get; set; } = new();
    }
}
