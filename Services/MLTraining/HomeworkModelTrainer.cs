using Microsoft.ML;
using QdratNew.Services.MLModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QdratNew.Services.MLTraining
{
    public class HomeworkModelTrainer
    {
        public string TrainAndSaveModel()
        {
            var mlContext = new MLContext();

            var trainingData = new List<HomeworkPerformanceInput>
    {
        new HomeworkPerformanceInput { AverageScore = 90, ErrorRate = 5, CompletionRate = 95 },
        new HomeworkPerformanceInput { AverageScore = 60, ErrorRate = 40, CompletionRate = 60 },
        new HomeworkPerformanceInput { AverageScore = 30, ErrorRate = 70, CompletionRate = 40 },
    };

            var data = mlContext.Data.LoadFromEnumerable(trainingData);

            var pipeline = mlContext.Transforms
                .CopyColumns("Label", nameof(HomeworkPerformanceInput.AverageScore))
                .Append(mlContext.Transforms.Concatenate("Features", nameof(HomeworkPerformanceInput.AverageScore), nameof(HomeworkPerformanceInput.ErrorRate), nameof(HomeworkPerformanceInput.CompletionRate)))
                .Append(mlContext.Regression.Trainers.FastTree());

            var model = pipeline.Fit(data);

            var modelPath = Path.Combine("MLModels", "HomeworkPerformanceModel.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            mlContext.Model.Save(model, data.Schema, modelPath);

            // 📊 تحليل تأثير كل ميزة
            var transformedData = model.Transform(data);
            var permutationMetrics = mlContext.Regression
                .PermutationFeatureImportance(model, transformedData, labelColumnName: "Label");

            var importanceReport = new List<string>();
            foreach (var kvp in permutationMetrics)
            {
                var featureName = kvp.Key;
                var stats = kvp.Value;

                importanceReport.Add($"{featureName}: MAE = {stats.MeanAbsoluteError:F4}, R² = {stats.RSquared:F4}");
            }


            File.WriteAllLines("MLModels/HomeworkImportanceReport.txt", importanceReport);

            return "✅ تم حفظ النموذج وتحليل التأثير في MLModels/HomeworkPerformanceModel.zip";
        }



    }
}
