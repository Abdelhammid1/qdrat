using Microsoft.ML.Data;

namespace QdratNew.AI.MLModels.Charts
{
    public class ChartPredictionOutput
    {
        [ColumnName("PredictedLabel")]
        public string Prediction { get; set; }
        public string PredictedLabel { get; set; }

        public float[] Score { get; set; }
    }
}
