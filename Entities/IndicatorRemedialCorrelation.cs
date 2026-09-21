using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class IndicatorRemedialCorrelation
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public Student Student { get; set; }

        [ForeignKey("Section")]
        public int SectionId { get; set; }
        public Section Section { get; set; }

        public double PreRemedialScore { get; set; }
        public double PostRemedialScore { get; set; }
        public double ImprovementPercent => Math.Round((PostRemedialScore - PreRemedialScore), 2);

    }
}
