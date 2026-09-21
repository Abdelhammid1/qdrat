using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class RemedialQuizQuestion
    {
      
            [Key]
            public int Id { get; set; }

            [ForeignKey("RemedialQuiz")]
            public int RemedialQuizId { get; set; }
            public RemedialQuiz RemedialQuiz { get; set; }

            [ForeignKey("Question")]
            public Guid QuestionId { get; set; }
            public Question Question { get; set; }
        }

   
}
