using Microsoft.ML;
using QdratNew.AI.MLModels.Branches;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QdratNew.AI.Analysis
{
    public static class BranchMLAnalyzer
    {
        private const string ModelPath = "MLModels/branch_model.zip";

        public static float PredictScore(List<BranchPerformanceData> data)
        {
            if (!File.Exists(ModelPath) || data.Count < 3)
                return -1;

            var mlContext = new MLContext();
            var model = mlContext.Model.Load(ModelPath, out _);

            var predictionEngine = mlContext.Model
                .CreatePredictionEngine<BranchPerformanceData, BranchPerformancePrediction>(model);

            var avg = new BranchPerformanceData
            {
                EngagementRate = data.Average(p => p.EngagementRate),
                AttendanceCount = data.Average(p => p.AttendanceCount),
                StudyHours = data.Average(p => p.StudyHours)
            };

            var prediction = predictionEngine.Predict(avg);
            return prediction.Score;
        }
    }
}
