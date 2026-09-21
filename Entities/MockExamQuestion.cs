using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class MockExamQuestion
    {
        public int Id { get; set; }

        public int MockExamId { get; set; }
        public MockExam MockExam { get; set; }

        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; }

        public string? StudentAnswer { get; set; }
        public bool? IsCorrect => StudentAnswer != null && StudentAnswer == Question.CorrectAnswer;
    }

}
