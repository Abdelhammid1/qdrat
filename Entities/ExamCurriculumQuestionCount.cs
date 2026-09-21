namespace QdratNew.Entities
{
    public class ExamCurriculumQuestionCount
    {
        public int Id { get; set; }

        public int ExamAssignmentId { get; set; }
        public ExamAssignmentToBatch ExamAssignment { get; set; }

        public int CurriculumId { get; set; }
        public Curriculum Curriculum { get; set; }

        public int QuestionCount { get; set; }
    }
}
