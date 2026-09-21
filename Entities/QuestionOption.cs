namespace QdratNew.Entities
{
    public class QuestionOption
    {
        public int Id { get; set; }

        public string? Text { get; set; }
        public string? ImageUrl { get; set; }

        public Guid QuestionId { get; set; }
        public Question Question { get; set; }
    }
}
