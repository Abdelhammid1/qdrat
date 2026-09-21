using Microsoft.ML;
using QdratNew.MLModels.Instructors;
using System.IO;

namespace QdratNew.Services.AI
{
    public class TeachingAIAnalyzer
    {
        private readonly string _modelPath = "MLModels/Instructors/BatchTeachingInsightModel.zip";
        private readonly MLContext _mlContext;
        private readonly PredictionEngine<BatchTeachingInsightInput, BatchTeachingInsightPrediction>? _engine;

        public TeachingAIAnalyzer()
        {
            _mlContext = new MLContext();

            if (File.Exists(_modelPath))
            {
                var model = _mlContext.Model.Load(_modelPath, out _);
                _engine = _mlContext.Model.CreatePredictionEngine<BatchTeachingInsightInput, BatchTeachingInsightPrediction>(model);
            }
        }

        public string Analyze(BatchTeachingInsightInput input)
        {
            if (_engine == null)
                return "❌ لم يتم تحميل النموذج أو لم يتم تدريبه بعد.";

            var prediction = _engine.Predict(input);
            return prediction.Recommendation ?? "⚠ لا توجد توصية حالية.";
        }
    }
}
