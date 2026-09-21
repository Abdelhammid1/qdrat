using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class RemedialVideoQuestion
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("RemedialVideo")]
        public int RemedialVideoId { get; set; }
        public RemedialVideo RemedialVideo { get; set; }

        [ForeignKey("Question")]
        public Guid QuestionId { get; set; }
        public Question Question { get; set; }
    }

}
