using QdratNew.ViewModels.Section;

namespace QdratNew.ViewModels.Instructor
{
    public class InstructorSmartEvaluationViewModel
    {
        public int InstructorId { get; set; }
        public string InstructorName { get; set; }

        // الأداء العام
        public double AIOverallScore { get; set; }
        public string AIEvaluationComment { get; set; }

        // تحليل المحاور التعليمية
        public List<SectionEvaluationAIViewModel> SectionsEvaluation { get; set; }

        // التوصيات الذكية
        public List<string> AIRecommendations { get; set; }

        // رسم بياني: أسماء المحاور + متوسط الدرجات
        public List<string> SectionTitles { get; set; }
        public List<double> AverageScores { get; set; }
    }

}
