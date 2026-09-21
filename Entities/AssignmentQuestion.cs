using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class AssignmentQuestion
    {
        public int Id { get; set; }

        public int AssignmentId { get; set; }
        public Assignment Assignment { get; set; }

        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        public int Order { get; set; }  // الترتيب داخل الواجب
    }

}
