using Microsoft.ML.Data;

namespace QdratNew.AI.MLModels.Charts
{
    public class ChartPredictionInput
    {
        [LoadColumn(0)]
        public float AverageValue { get; set; }

        [LoadColumn(1)]
        public float MaxValue { get; set; }

        [LoadColumn(2)]
        public float MinValue { get; set; }

        [LoadColumn(3)]
        public float StdDeviation { get; set; }

        [LoadColumn(4)]
        public float Count { get; set; }

        [LoadColumn(5)]
        public string Label { get; set; } = "";
    }
}
