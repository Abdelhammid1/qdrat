using QdratNew.ViewModels.Exam;

namespace QdratNew.ViewModels.Instructor
{
    public class WeakSectionViewModel
    {
        public string SectionTitle { get; set; }
        public string CurriculumTitle { get; set; }
        public int ErrorCount { get; set; }
        public List<string> FailedIndicators { get; set; } = new();
        public List<IndicatorAnalysisViewModel> Indicators { get; set; } = new();
        public List<QuestionReviewItem> WrongQuestions { get; set; } = new();
    }

    public class IndicatorAnalysisViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        // 📊 عدد الأسئلة في الواجبات والاختبارات
        public int HomeworkQuestionCount { get; set; }
        public int ExamQuestionCount { get; set; }

        // ❌ الأسئلة التي أخطأ فيها الطالب داخل هذا المؤشر
        public List<QuestionReviewItem> WrongQuestions { get; set; } = new();
    }


}
