using System;
using System.Linq;
using System.IO;
using Microsoft.ML;
using Microsoft.ML.Data;
using QdratNew.Data;
using QdratNew.Models;
using QdratNew.AI.MLModels.Students;

namespace QdratNew.MLModels
{
    public class MLModelTrainer
    {
        private readonly MLContext _mlContext;
        private ITransformer _model;
        private const string ModelPath = "MLModels/student_model.zip";

        public MLModelTrainer()
        {
            _mlContext = new MLContext();
        }

        public void TrainAndSaveModel(ApplicationDbContext context)
        {
            Console.WriteLine("🔍 بدء تدريب نموذج الذكاء الاصطناعي...");

            var data = context.StudentPerformances
                .Select(sp => new StudentPerformanceData
                {
                    PreviousScore = (float)sp.Score,
                    StudyHours = sp.StudyHours,
                    ExercisesCompleted = sp.ExercisesCompleted,
                    AttendanceCount = sp.AttendanceCount,
                    EngagementRate = sp.EngagementRate
                }).ToList();

            if (data.Count == 0)
            {
                Console.WriteLine("❌ لا يوجد بيانات كافية لتدريب النموذج.");
                return;
            }

            var dataView = _mlContext.Data.LoadFromEnumerable(data);

            var pipeline = _mlContext.Transforms.CopyColumns(outputColumnName: "Label", inputColumnName: "PreviousScore")
                .Append(_mlContext.Transforms.Concatenate("Features", "PreviousScore", "StudyHours", "ExercisesCompleted", "AttendanceCount", "EngagementRate"))
                .Append(_mlContext.Regression.Trainers.Sdca());

            _model = pipeline.Fit(dataView);

            // 🔹 حفظ النموذج في ملف
            _mlContext.Model.Save(_model, dataView.Schema, ModelPath);
            Console.WriteLine("✅ تم حفظ نموذج الذكاء الاصطناعي في: " + ModelPath);
        }
    }

   
}
