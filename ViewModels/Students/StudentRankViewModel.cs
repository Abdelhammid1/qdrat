namespace QdratNew.ViewModels.Students
{
    public class StudentRankViewModel
    {
        public int Rank { get; set; }
        public int TotalStudents { get; set; }
        public string MedalImageUrl { get; set; } // gold.png / silver.png / bronze.png
        public string MotivationalMessage { get; set; }
        public string PreviousStudentName { get; set; }
        public double? PreviousStudentScore { get; set; }
        public string NextStudentName { get; set; }
        public double? NextStudentScore { get; set; }
        public DateTime LastUpdated { get; set; }
    }

}
