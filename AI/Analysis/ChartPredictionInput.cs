namespace QdratNew.AI.Analysis
{
    public class ChartPredictionInput
    {
        public float AverageValue { get; set; }
        public float MaxValue { get; set; }
        public float MinValue { get; set; }
        public float StdDeviation { get; set; }
        public float Count { get; set; }

        public string Label { get; set; }
    }

}