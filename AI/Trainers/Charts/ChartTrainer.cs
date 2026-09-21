using Microsoft.ML;
using QdratNew.AI.MLModels.Charts;
using System;
using System.IO;

namespace QdratNew.AI.Trainers.Charts
{
    public static class ChartTrainer
    {
        public static void TrainAndSaveModel()
        {
            var mlContext = new MLContext();

            string dataPath = Path.Combine("MLModels", "Charts", "ChartTrainingData.tsv");
            string modelPath = Path.Combine("MLModels", "Charts", "ChartAnalyzerModel.zip");

            if (!File.Exists(dataPath))
            {
                Console.WriteLine("❌ لم يتم العثور على ملف التدريب: " + dataPath);
                return;
            }

            // 1. تحميل البيانات
            var dataView = mlContext.Data.LoadFromTextFile<ChartPredictionInput>(
                dataPath,
                hasHeader: true,
                separatorChar: ';');

            // 2. إعداد pipeline التدريب
            var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label")
                .Append(mlContext.Transforms.Concatenate("Features", nameof(ChartPredictionInput.AverageValue), nameof(ChartPredictionInput.MaxValue),
                                                         nameof(ChartPredictionInput.MinValue), nameof(ChartPredictionInput.StdDeviation), nameof(ChartPredictionInput.Count)))
                .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // 3. تدريب النموذج
            var model = pipeline.Fit(dataView);

            // 4. حفظ النموذج
            mlContext.Model.Save(model, dataView.Schema, modelPath);
            Console.WriteLine("✅ تم حفظ النموذج بنجاح في: " + modelPath);
        }
    }
}
