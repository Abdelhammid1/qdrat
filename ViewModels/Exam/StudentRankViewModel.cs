
namespace QdratNew.ViewModels.Exam
{
    public class StudentRankViewModel
    {
        public int Rank { get; set; }
        public int TotalStudents { get; set; }

        public string MotivationalMessage { get; set; } = string.Empty;

        // رابط صورة الميدالية (اختياري)
        public string? MedalImageUrl { get; set; }
        public DateTime LastUpdated { get; internal set; }
    }
}
