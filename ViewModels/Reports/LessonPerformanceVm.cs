
namespace QdratNew.ViewModels.Reports
{
    public class LessonPerformanceVm
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public string SectionTitle { get; set; } = string.Empty;
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public double AccuracyPercent { get; set; }
        public List<LessonPerformanceVm> Lessons { get; set; } = new(); // ✅ نفس namespace
        public double SuccessRate { get; internal set; }
        public double ExamSuccessRate { get; internal set; }
        public DateTime LastActivityDate { get; internal set; }
        public double HomeworkSuccessRate { get; internal set; }

     

    }
}
