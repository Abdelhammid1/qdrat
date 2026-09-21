using QdratNew.Enums;

namespace QdratNew.ViewModels.Partner.Exam
{
    public class ConfirmBatchExamVM
    {
        public string ExamTitle { get; set; }

        public int CurriculumId { get; set; }

        public ExamGenerateScope Scope { get; set; }

        public int BatchId { get; set; }

        public List<Guid> QuestionIds { get; set; } = new();

        public DateTime? ScheduledDate { get; set; }
        public DateTime? EndAt { get; set; }
        public int ExamId { get; set; }
        

    }

}
