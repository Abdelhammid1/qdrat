using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class PerformanceIndicatorExamToBatch
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("PerformanceIndicatorExam")]
        public int PerformanceIndicatorExamId { get; set; }
        public PerformanceIndicatorExam PerformanceIndicatorExam { get; set; }

        [ForeignKey("Batch")]
        public int BatchId { get; set; }
        public Batch Batch { get; set; }



    }
}
