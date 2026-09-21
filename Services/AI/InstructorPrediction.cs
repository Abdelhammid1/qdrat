using Microsoft.ML.Data;

namespace QdratNew.Services.AI
{
    public class InstructorPrediction
    {
        [ColumnName("PredictedLabel")]
        public string Recommendation;
    }
}
