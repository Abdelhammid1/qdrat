namespace QdratNew.Entities
{
    public class ProfessionalModelQuestion
    {
        public int Id { get; set; }

        public int ModelId { get; set; }
        public ProfessionalModel Model { get; set; }

        public Guid? QuestionId { get; set; }
        public Question Question { get; set; }

        public int OrderNumber { get; set; }
    }
}
