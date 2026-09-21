namespace QdratNew.Modules.QuestionBank.Insights.Dtos
{
    public class QuestionBankInsightsDto
    {
        public OverallStatusDto OverallStatus { get; set; } = new();
        public QualityDto Quality { get; set; } = new();
        public List<CurriculumCoverageDto> CurriculumCoverage { get; set; } = new();
    }

    public class OverallStatusDto
    {
        public int Approved { get; set; }
        public int ReadyForReview { get; set; }
        public int MissingAnswer { get; set; }
        public int Incomplete { get; set; }
    }

    public class QualityDto
    {
        public int Complete { get; set; }
        public int Reviewed { get; set; }
        public int WithCorrectAnswer { get; set; }
    }

    public class CurriculumCoverageDto
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }
}
