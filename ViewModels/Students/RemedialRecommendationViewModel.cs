namespace QdratNew.ViewModels.Students
{
    public class RemedialRecommendationViewModel
    {
        public int SectionId { get; set; }                    // ✅ معرف المحور

        public string SectionTitle { get; set; } // اسم المحور
        public string RecommendationText { get; set; } // النص التوصي
        public int SuggestedDays { get; set; } // المدة الزمنية المقترحة
    }
}
