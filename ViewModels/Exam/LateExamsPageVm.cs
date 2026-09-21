namespace QdratNew.ViewModels.Exam
{
    public class LateExamsPageVm
    {

        public List<StudentExamCardViewModel> Late { get; set; } = new();
        public List<StudentExamCardViewModel> ExpiringSoon { get; set; } = new();
        public List<StudentExamCardViewModel> Upcoming { get; set; } = new();

        public int TotalLate { get; set; }
        public int TotalExpiringSoon { get; set; }
        public int TotalUpcoming { get; set; }

    }


}
