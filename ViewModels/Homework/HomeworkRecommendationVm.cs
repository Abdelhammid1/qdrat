namespace QdratNew.ViewModels.Homework
{
    public class HomeworkRecommendationVm
    {
        public string PerformanceSummary { get; set; } = string.Empty;
        public string TimeNote { get; set; } = string.Empty;
        public List<string> SectionTips { get; set; } = new();
        public List<string> LessonTips { get; set; } = new();
        public string FinalAdvice { get; set; } = string.Empty;


        // 🔹 الخصائص الجديدة لتوحيد التحليل مع الاختبارات
        public string SpeedLabel { get; set; } = string.Empty;      // تصنيف السرعة (سريع جدًا / مناسب / بطيء)



        // 🔹 نصوص أساسية عن الأداء العام
        public string SpeedNote { get; set; } = string.Empty;

        // 🔹 ملاحظات فردية أو نصائح مخصصة
        public List<string> IndividualTips { get; set; } = new List<string>();

        // 🔹 نسبة التوقع أو مستوى النجاح المتوقع
        public double SuccessProbability { get; set; }

        // 🔹 عنوان مختصر أو تصنيف السرعة (بطيء / معتدل / سريع)

    }
}
