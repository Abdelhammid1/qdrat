namespace QdratNew.ViewModels.Exam
{
    public class IndividualExamDetailsVm
    {
        public int AssignmentId { get; set; }
        public int ExamId { get; set; }
        public string ExamTitle { get; set; }
        public string ReferenceCode { get; set; }

        public int StudentId { get; set; }
        public string StudentName { get; set; }

        public DateTime ScheduledDate { get; set; }
        public DateTime EndAt { get; set; }
        public int DurationMinutes { get; set; }

        public string CurriculumTitle { get; set; }
        public string? SectionTitle { get; set; }

        public List<QuestionRowVm> Questions { get; set; } = new();
    }

    public class QuestionRowVm
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public string Difficulty { get; set; }
        public int Order { get; set; }
        public int? SectionId { get; set; }
        public string SectionTitle { get; internal set; }
        public string InternalNote { get; internal set; }
    }
}
