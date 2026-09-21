using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class PerformanceIndicatorExamSection
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("PerformanceIndicatorExam")]
        public int PerformanceIndicatorExamId { get; set; }
        public PerformanceIndicatorExam PerformanceIndicatorExam { get; set; }

        [ForeignKey("Section")]
        public int SectionId { get; set; }
        public Section Section { get; set; }

        public int QuestionCount { get; set; }
        public double Score { get; set; }
        public ICollection<PerformanceIndicatorExamQuestion> Questions { get; set; } = new List<PerformanceIndicatorExamQuestion>();

    }
}
