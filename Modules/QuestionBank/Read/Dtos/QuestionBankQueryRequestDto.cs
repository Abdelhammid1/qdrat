namespace QdratNew.Modules.QuestionBank.Read.Dtos
{
    public class QuestionBankQueryRequestDto
    {
        public int? CurriculumId { get; set; }
        public int? SectionId { get; set; }
        public int? LessonId { get; set; }

        public bool IncludeIncomplete { get; set; } = true;
        public bool IncludeWithoutAnswer { get; set; } = true;
        public bool OnlyApproved { get; set; } = false;

        public string? SearchText { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string? Status { get; internal set; }
    }
}
