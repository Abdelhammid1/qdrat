namespace QdratNew.Modules.QuestionBank.Read.Dtos
{
    public class QuestionBankItemDto
    {
        public Guid Id { get; set; }
        public string ReferenceNumber { get; set; }
        public string Title { get; set; }

        public bool IsComplete { get; set; }
        public bool HasCorrectAnswer { get; set; }
        public bool IsReviewed { get; set; }

        public string CurriculumTitle { get; set; }
        public string SectionTitle { get; set; }
        public string LessonTitle { get; set; }
        public bool IsRTL { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? Status { get; set; } // Incomplete / MissingAnswer / Ready / Approved
        public string? InternalNote { get; internal set; }
    }
}
