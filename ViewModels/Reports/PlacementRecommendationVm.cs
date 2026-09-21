namespace QdratNew.ViewModels.Reports
{
    public class PlacementRecommendationVm
    {
        // 🔹 عنوان التوصية الرئيسية
        public string TrackTitle { get; set; }

        // 🔹 الملاحظة أو نص التوصية
        public string TrackNote { get; set; }

        // 🔹 تقييم السرعة (مثل: سريع / متوسط / بطيء)
        public string SpeedLabel { get; set; }

        // 🔹 ملاحظة إضافية عن السرعة والدقة
        public string SpeedNote { get; set; }

        // 🔹 يمكن لاحقًا إضافة عناصر فرعية (بطاقات التوصية)
        public string TrackCard1 { get; set; }
        public string TrackCard1Desc { get; set; }
        public string TrackCard2 { get; set; }
        public string TrackCard2Desc { get; set; }

        // 🔹 قائمة التوصيات الفردية (مثل نصائح المذاكرة)
        public List<string> IndividualTips { get; set; } = new();
    }
}
