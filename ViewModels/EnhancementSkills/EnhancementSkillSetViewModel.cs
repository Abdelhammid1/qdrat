namespace QdratNew.ViewModels.EnhancementSkills
{
    public class EnhancementSkillSetViewModel
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string LectureTitle { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EndAt { get; set; }
        public DateTime? SentAt { get; set; }
        public int StudentsCount { get; set; }
        public int QuestionsCount { get; set; }
        public int AnsweredCount { get; set; }
        public int QuestionsPerStudent { get; set; }
        public bool IsSent { get; set; }
        public bool IsArchived { get; set; }
        public int GenerationMethod { get; set; }
        public DateTime? ScheduledSendAt { get; set; }

        public int AnsweredPercentage =>
            StudentsCount > 0 && QuestionsPerStudent > 0
                ? (int)Math.Round(AnsweredCount * 100.0 / (StudentsCount * QuestionsPerStudent))
                : 0;
    }

    public class EnhancementBatchCardVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseTitle { get; set; } = "";
        public int TotalStudents { get; set; }
        public int TotalSets { get; set; }
        public int SentSets { get; set; }
        public int PendingSets { get; set; }
        public int ArchivedSets { get; set; }
        public int TotalAssignments { get; set; }
        public int AnsweredAssignments { get; set; }
        public int ResponseRate => TotalAssignments > 0 ? (int)Math.Round(AnsweredAssignments * 100.0 / TotalAssignments) : 0;
    }

    public class EnhancementIndexVm
    {
        public List<EnhancementBatchCardVm> BatchCards { get; set; } = new List<EnhancementBatchCardVm>();
        public List<EnhancementSkillSetViewModel> Sets { get; set; } = new List<EnhancementSkillSetViewModel>();
        public List<EnhancementSkillSetViewModel> ArchivedSets { get; set; } = new List<EnhancementSkillSetViewModel>();
        public int TotalSets { get; set; }
        public int SentSets { get; set; }
        public int PendingSets { get; set; }
        public int ArchivedCount { get; set; }
        public int TotalStudentsReached { get; set; }
        public int OverallResponseRate { get; set; }
    }

    public class BatchEnhancementDetailsVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseTitle { get; set; } = "";
        public int TotalStudents { get; set; }
        public List<EnhancementSkillSetViewModel> Sets { get; set; } = new List<EnhancementSkillSetViewModel>();
        public List<EnhancementSkillSetViewModel> ArchivedSets { get; set; } = new List<EnhancementSkillSetViewModel>();
    }
}
