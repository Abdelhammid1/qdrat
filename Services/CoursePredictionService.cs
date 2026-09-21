using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using QdratNew.Entities;
using QdratNew.Data;
using QdratNew.Models;

namespace QdratNew.Services
{
    public class CoursePredictionService
    {
        private readonly ApplicationDbContext _context;
        private readonly MLContext _mlContext;
        private ITransformer _model;

        public CoursePredictionService(ApplicationDbContext context)
        {
            _context = context;
            _mlContext = new MLContext();
            TrainModel(); // ✅ تدريب النموذج عند بدء الخدمة
        }

        private void TrainModel()
        {
            var data = _context.StudentCourses
                .GroupBy(sc => new { Month = sc.DateEnrolled.Month })
                .Select(g => new CoursePredictionModel
                {
                    Month = g.Key.Month,
                    Registrations = g.Count()
                })
                .ToList();

            if (!data.Any())
            {
                Console.WriteLine("⚠️ لا توجد بيانات تدريبية، يتم تخطي التدريب!");
                return; // ✅ تجنب فشل `ML.NET` بسبب نقص البيانات
            }

            var dataView = _mlContext.Data.LoadFromEnumerable(data);

            var pipeline = _mlContext.Transforms.Concatenate("Features", new[] { "Month" })
                .Append(_mlContext.Regression.Trainers.Sdca(labelColumnName: "Registrations", maximumNumberOfIterations: 100));

            _model = pipeline.Fit(dataView);
        }


        public float Predict(int month)
        {
            var predictionEngine = _mlContext.Model.CreatePredictionEngine<CoursePredictionModel, CoursePrediction>(_model);
            var prediction = predictionEngine.Predict(new CoursePredictionModel { Month = month });
            return prediction.PredictedRegistrations;
        }
    }
}
