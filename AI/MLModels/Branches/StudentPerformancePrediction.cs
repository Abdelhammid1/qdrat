using Microsoft.ML.Data;

namespace QdratNew.AI.MLModels.Students
{
    public class StudentPerformancePrediction
    {
        [ColumnName("Score")]
        public float PredictedScore { get; set; }
    }
}
