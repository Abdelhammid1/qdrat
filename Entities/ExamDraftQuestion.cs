using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class ExamDraftQuestion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExamDraftId { get; set; }

        [Required]
        public Guid QuestionId { get; set; }

        public int Order { get; set; }

        [ForeignKey(nameof(ExamDraftId))]
        public ExamDraft ExamDraft { get; set; }

        [ForeignKey(nameof(QuestionId))]
        public Question Question { get; set; }
    }
}
