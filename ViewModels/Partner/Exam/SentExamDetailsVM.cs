namespace QdratNew.ViewModels.Partner.Exam
{
    public class SentExamDetailsVM
    {


        public int ExamId { get; set; }          // مضمون الوجود
        public string Title { get; set; }

        public int BatchId { get; set; }
        public string BatchName { get; set; }

        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        public DateTime SentAt { get; set; }
        public int StudentsCount { get; set; }

        public List<ExamDetailsQuestionVM> Questions { get; set; } = new();
        public int DurationMinutes { get; set; }

    }
}
