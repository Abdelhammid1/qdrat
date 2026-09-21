namespace QdratNew.ViewModels.Students
{
    public class StudentAIAnalysisInput
    {
        public float SuccessRate { get; set; }                   // نسبة النجاح
        public float SessionCompletionRatio { get; set; }        // نسبة إكمال الجلسات
        public float AverageHomeworkTime { get; set; }           // متوسط وقت حل الواجبات (بالثواني)
        public int TotalAssignments { get; set; }                // عدد الواجبات
        public int TotalExams { get; set; }                      // عدد الاختبارات
    }
}
