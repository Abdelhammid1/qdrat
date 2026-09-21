namespace QdratNew.Entities
{
    public class InstructorModelQuestion
    {
        public int Id { get; set; }

        public int InstructorModelId { get; set; }
        public InstructorModel InstructorModel { get; set; }

        public Guid QuestionId { get; set; }

        public int Order { get; set; }
    }
}