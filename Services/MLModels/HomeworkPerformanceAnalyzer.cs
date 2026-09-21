using Microsoft.ML;
using QdratNew.Services.MLModels;

namespace QdratNew.Services.MLModels
{
    public class HomeworkPerformanceAnalyzer
    {
        private readonly MLContext _mlContext;
        private PredictionEngine<HomeworkPerformanceInput, HomeworkPerformanceOutput> _engine;

        public HomeworkPerformanceAnalyzer()
        {
            _mlContext = new MLContext();

            // يفترض أنك درّبت النموذج وحفظته بصيغة zip
            var modelPath = "MLModels/HomeworkPerformanceModel.zip";
            DataViewSchema schema;
            var model = _mlContext.Model.Load(modelPath, out schema);

            _engine = _mlContext.Model.CreatePredictionEngine<HomeworkPerformanceInput, HomeworkPerformanceOutput>(model);
        }

        public string Analyze(float avgScore, float errorRate, float completionRate)
        {
            var input = new HomeworkPerformanceInput
            {
                AverageScore = avgScore,
                ErrorRate = errorRate,
                CompletionRate = completionRate
            };

            var result = _engine.Predict(input);
            return result.Recommendation;
        }
    }
}
