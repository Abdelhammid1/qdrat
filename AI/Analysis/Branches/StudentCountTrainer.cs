using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;

namespace QdratNew.AI.MLModels.Branches
{
    public class StudentCountData
    {
        public float MonthIndex { get; set; }  // مثال: 1, 2, 3, ...
        public float StudentCount { get; set; } // عدد الطلاب في ذلك الشهر
    }

    public class StudentCountForecast
    {
        [ColumnName("Score")]
        public float PredictedCount { get; set; }
    }

    public static class StudentCountTrainer
    {
        private static readonly string ModelPath = Path.Combine("MLModels", "Branches", "StudentCountModel.zip");

        public static void TrainAndSaveModel(List<float> monthlyCounts)
        {
            var mlContext = new MLContext();

            var trainingData = new List<StudentCountData>();
            for (int i = 0; i < monthlyCounts.Count; i++)
            {
                trainingData.Add(new StudentCountData
                {
                    MonthIndex = i + 1,
                    StudentCount = monthlyCounts[i]
                });
            }

            var dataView = mlContext.Data.LoadFromEnumerable(trainingData);

            var pipeline = mlContext.Transforms.CopyColumns("Label", nameof(StudentCountData.StudentCount))
                .Append(mlContext.Transforms.Concatenate("Features", nameof(StudentCountData.MonthIndex)))
                .Append(mlContext.Regression.Trainers.Sdca());

            var model = pipeline.Fit(dataView);

            Directory.CreateDirectory(Path.GetDirectoryName(ModelPath));
            mlContext.Model.Save(model, dataView.Schema, ModelPath);

            Console.WriteLine($"✅ تم حفظ نموذج توقع عدد الطلاب في: {ModelPath}");
        }

        public static float PredictNext(int nextMonthIndex)
        {
            var mlContext = new MLContext();

            if (!File.Exists(ModelPath))
                throw new FileNotFoundException("نموذج عدد الطلاب غير موجود");

            var model = mlContext.Model.Load(ModelPath, out _);
            var engine = mlContext.Model.CreatePredictionEngine<StudentCountData, StudentCountForecast>(model);

            var prediction = engine.Predict(new StudentCountData
            {
                MonthIndex = nextMonthIndex
            });

            return prediction.PredictedCount;
        }
    }
}
