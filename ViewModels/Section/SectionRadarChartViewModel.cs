namespace QdratNew.ViewModels.Section
{
    public class SectionRadarChartViewModel
    {
        public List<string> Labels { get; set; } = new();
        public List<double> StudentScores { get; set; } = new();
        public List<double> BatchAverages { get; set; } = new();
    }

}
