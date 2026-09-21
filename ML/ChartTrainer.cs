using Microsoft.ML;
using QdratNew.AI.MLModels.Charts;
using System;
using System.IO;

namespace QdratNew.ML
{
    public static class ChartTrainer
    {
        public static string TrainModel()
        {
            var mlContext = new MLContext();

            var dataPath = Path.Combine("MLModels", "Charts", "ChartTrainingData.tsv");
            var modelPath = Path.Combine("MLModels", "Charts", "ChartAnalyzerModel.zip");

            if (!File.Exists(dataPath))
                return "❌ لم يتم العثور على ملف بيانات التدريب.";

            try
            {
                var data = mlContext.Data.LoadFromTextFile<ChartPredictionInput>(
                    path: dataPath,
                    hasHeader: true,
                    separatorChar: ';');

                var pipeline = mlContext.Transforms.Conversion
                    .MapValueToKey("Label")
                    .Append(mlContext.Transforms.Concatenate("Features",
                        nameof(ChartPredictionInput.AverageValue),
                        nameof(ChartPredictionInput.MaxValue),
                        nameof(ChartPredictionInput.MinValue),
                        nameof(ChartPredictionInput.StdDeviation),
                        nameof(ChartPredictionInput.Count)))
                    .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                    .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

                var model = pipeline.Fit(data);
                mlContext.Model.Save(model, data.Schema, modelPath);

                return "✅ تم تدريب النموذج بنجاح وتخزينه في: " + modelPath;
            }
            catch (Exception ex)
            {
                return "❌ حدث خطأ أثناء التدريب: " + ex.Message;
            }
        }
    }
}
