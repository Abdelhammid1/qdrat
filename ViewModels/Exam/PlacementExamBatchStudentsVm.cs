namespace QdratNew.ViewModels.Exam
{
    public class PlacementExamBatchStudentsVm
    {
        public int ExamId { get; set; }
        public int BatchId { get; set; }

        public List<PlacementExamStudentVm> Students { get; set; } = new();
    }

}
