namespace QdratNew.ViewModels.Instructor
{
    public class AtRiskStudentViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = "";
        public double AvgScore { get; set; }
        public int ExamCount { get; set; }
        public string BatchName { get; set; } = "";
        public double Score { get; internal set; }
    }
}
