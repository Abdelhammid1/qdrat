using QdratNew.Enums;

namespace QdratNew.ViewModels.Partner.Exam
{
    public class ConfirmStudentExamVM
    {
        public int ExamId { get; set; }


        public string ExamTitle { get; set; }

        public int CurriculumId { get; set; }

        public ExamGenerateScope Scope { get; set; }

        public int StudentId { get; set; }

        public List<Guid> QuestionIds { get; set; } = new();

        public DateTime? ScheduledDate { get; set; }
        public DateTime? EndAt { get; set; }



    }

}
