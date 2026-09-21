namespace QdratNew.ViewModels.Exam
{
    public class StudentRankVm
    {
        public int Rank { get; set; }
        public int TotalStudents { get; set; }
        public DateTime LastUpdated { get; set; }
        public string? MedalImageUrl { get; set; }
        public string? MotivationalMessage { get; set; }
    }
}
