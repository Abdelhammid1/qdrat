using Microsoft.ML;
using QdratNew.AI.MLModels.Charts;
using System;
using System.Collections.Generic;
using System.IO;

namespace QdratNew.AI.MLModels.Charts
{
    public static class ChartModelTrainer
    {
        public static void TrainAndSave()
        {
            var mlContext = new MLContext();

            // ✅ إنشاء المجلد إذا لم يكن موجودًا
            var modelFolder = Path.Combine("MLModels", "Charts");
            Directory.CreateDirectory(modelFolder);

            // ✅ بيانات تدريب تجريبية
            var trainingData = new List<ChartPredictionInput>
{
    new ChartPredictionInput { AverageValue = 75, MaxValue = 90, MinValue = 60, StdDeviation = 8, Count = 12, Label = "مستقر" },
    new ChartPredictionInput { AverageValue = 40, MaxValue = 55, MinValue = 20, StdDeviation = 15, Count = 10, Label = "خطر" },
    new ChartPredictionInput { AverageValue = 85, MaxValue = 98, MinValue = 70, StdDeviation = 5, Count = 15, Label = "نمو" },
    new ChartPredictionInput { AverageValue = 45, MaxValue = 60, MinValue = 30, StdDeviation = 10, Count = 9, Label = "تراجع" },
};

            var dataView = mlContext.Data.LoadFromEnumerable(trainingData);

            var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label")
                .Append(mlContext.Transforms.Concatenate("Features",
                    nameof(ChartPredictionInput.AverageValue),
                    nameof(ChartPredictionInput.MaxValue),
                    nameof(ChartPredictionInput.MinValue),
                    nameof(ChartPredictionInput.StdDeviation),
                    nameof(ChartPredictionInput.Count)))
                .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var model = pipeline.Fit(dataView);

            var modelPath = Path.Combine(modelFolder, "ChartAnalyzerModel.zip");
            mlContext.Model.Save(model, dataView.Schema, modelPath);

            Console.WriteLine("✅ تم تدريب النموذج وتخزينه في: " + modelPath);
        }
    }
}
