
using Microsoft.ML;
using QdratNew.AI.MLModels.Charts;
using QdratNew.ViewModels.AI;
using QdratNew.ViewModels.Students;
using System.IO;

namespace QdratNew.Services.AI
{
    public class ChartPredictionService
    {
        private readonly string modelPath = Path.Combine("MLModels", "Charts", "ChartAnalyzerModel.zip");
        private readonly MLContext _mlContext;
        private readonly PredictionEngine<ChartPredictionInput, ChartPredictionOutput> _engine;

        public ChartPredictionService()
        {
            _mlContext = new MLContext();

            if (!File.Exists(modelPath))
                throw new FileNotFoundException("❌ لم يتم العثور على نموذج ML.");

            DataViewSchema schema;
            var model = _mlContext.Model.Load(modelPath, out schema);

            _engine = _mlContext.Model.CreatePredictionEngine<ChartPredictionInput, ChartPredictionOutput>(model);
        }

        public string PredictLabel(ChartAnalysisInputViewModel input)
        {
            var chartInput = new ChartPredictionInput
            {
                AverageValue = input.AverageHomeworkTime,
                MaxValue = input.SuccessRate,
                MinValue = input.RiskScore,
                StdDeviation = input.SessionCompletionRatio,
                Count = input.TotalAssignments
            };

            var prediction = _engine.Predict(chartInput);
            return prediction.PredictedLabel;
        }
    }
}
