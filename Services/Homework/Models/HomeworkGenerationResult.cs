using System;

namespace QdratNew.Services.Homework.Models
{
    public class HomeworkGenerationResult
    {
        public bool Success { get; set; }

        public int HomeworkId { get; set; }

        public int QuestionsCount { get; set; }

        /// <summary>
        /// هل يحتاج مراجعة قبل الإرسال
        /// </summary>
        public bool RequiresReview { get; set; }

        /// <summary>
        /// رابط صفحة المراجعة (إن وجد)
        /// </summary>
        public string? ReviewUrl { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
