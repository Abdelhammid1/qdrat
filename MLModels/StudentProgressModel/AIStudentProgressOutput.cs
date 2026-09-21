using Microsoft.ML.Data;

namespace QdratNew.MLModels.StudentProgressModel
{
    public class AIStudentProgressOutput
    {
        [ColumnName("PredictedLabel")]
        public string RecommendationText { get; set; } = "";
    }
}
