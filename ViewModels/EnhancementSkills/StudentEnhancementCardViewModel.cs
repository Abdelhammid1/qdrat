using System;

namespace QdratNew.ViewModels.EnhancementSkills
{
    public class StudentEnhancementCardViewModel
    {
        public int SetId { get; set; }
        public string Title { get; set; } = "";
        public string? LectureTitle { get; set; }
        public string? SectionNames { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EndAt { get; set; }
        public int QuestionsCount { get; set; }
        public bool IsSubmitted { get; set; }
        public double Score { get; set; }
        public double BatchAverageScore { get; set; }
        public string DelayLevel { get; set; } = "";
    }
}
