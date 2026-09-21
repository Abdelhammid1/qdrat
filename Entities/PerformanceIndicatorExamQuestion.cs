using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class PerformanceIndicatorExamQuestion
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("PerformanceIndicatorExam")]
        public int PerformanceIndicatorExamId { get; set; }
        public PerformanceIndicatorExam PerformanceIndicatorExam { get; set; }

        [ForeignKey("Question")]
        public Guid QuestionId { get; set; }
        public Question Question { get; set; }
        // ✅ الجديد: ربط السؤال بالمحور الذي أُضيف منه
        [ForeignKey("Section")]
        public int? SectionId { get; set; }
        public Section Section { get; set; }

        public int OrderNumber { get; set; }
    }
}
