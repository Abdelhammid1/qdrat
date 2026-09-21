using QdratNew.Enums;

namespace QdratNew.ViewModels.Students
{
    public class StudentActivityLogViewModel
    {
        public StudentActivityType ActivityType { get; set; }
        public string ActivityTitle { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool? WasCorrect { get; set; }
        public int? Score { get; set; }
        public string? Note { get; set; }
        public float? ConfidenceScore { get; set; }
        public bool IsPredictedWeakness { get; set; }

        // ✅ إضافات مهمة لعرض المؤشر والمحور
        public string? SectionTitle { get; set; }
        public string? LessonTitle { get; set; }
        public string? VideoUrl { get; set; } // 🔗 رابط الفيديو لشرح السؤال

        public string? StudentAnswer { get; set; }
        public string? CorrectAnswer { get; set; }

    }
}
