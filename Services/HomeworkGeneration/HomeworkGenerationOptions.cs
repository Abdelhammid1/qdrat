namespace QdratNew.Services.HomeworkGeneration
{
    public class HomeworkGenerationOptions
    {
        // عدد الأسئلة لكل مؤشر
        public int QuestionsPerLesson { get; set; } = 5;

        // السماح بالتكرار أم لا
        public bool AvoidPreviouslySolvedQuestions { get; set; } = true;

        // خلط الأسئلة
        public bool ShuffleQuestions { get; set; } = true;

        // إتاحة المراجعة قبل الإرسال
        public bool RequireReviewBeforePublish { get; set; } = true;

        // زمن الواجب (اختياري)
        public int? TimeLimitInMinutes { get; set; }
    }
}
