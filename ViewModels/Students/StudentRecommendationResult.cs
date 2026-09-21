namespace QdratNew.ViewModels.Students
{
    public class StudentRecommendationResult
    {
        public List<string> SuggestedLessons { get; set; } = new();
        public List<string> SuggestedExams { get; set; } = new();
        public string MotivationMessage { get; set; } = "";


        public int StudentId { get; set; }

        // 📝 قائمة التوصيات الموجهة للطالب
        public List<string> Recommendations { get; set; } = new List<string>();

        // 🔮 نسبة النجاح المتوقعة (اختياري)
        public double SuccessProbability { get; set; }

        // ⏱️ تاريخ إنشاء التوصية
        public DateTime GeneratedAt { get; set; } = DateTime.Now;


    }
}
