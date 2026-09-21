namespace QdratNew.ViewModels.Question
{
    public class QuestionViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string TitlePreview { get; set; }
        public bool IsReviewed { get; set; }
        public DateTime CreatedAt { get; set; }

        public string CurriculumTitle { get; set; }
        public string SectionTitle { get; set; }
        public string LessonTitle { get; set; }

        public bool IsQuantitative { get; set; }
        public bool IsComplete { get; set; }           // ✅ هل السؤال مكتمل
        public bool IsRejected { get; set; }           // ❌ هل تم رفضه
        public bool IsReadyForBank { get; set; }       // ✅ هل مؤهل للظهور في بنك الأسئلة
        public string? LastModifiedBy { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        public string? CorrectAnswer { get; set; }

        public List<QuestionAuditLogViewModel>? AuditLogItems { get; set; }

        public List<string>? SelectedLabels { get; set; }
    }
    public class QuestionAuditLogViewModel
    {
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public DateTime PerformedAt { get; set; }
    }

}
