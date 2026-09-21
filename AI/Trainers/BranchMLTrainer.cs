using Microsoft.ML;
using QdratNew.AI.MLModels.Branches;
using QdratNew.Data;
using System.IO;
using System.Linq;

namespace QdratNew.AI.Trainers
{
    public static class BranchMLTrainer
    {
        private const string ModelPath = "MLModels/branch_model.zip";

        public static void TrainAndSaveModel(ApplicationDbContext context)
        {
            var mlContext = new MLContext();

            var data = context.StudentPerformances
                .Where(p => p.Score > 0)
                .Select(p => new BranchPerformanceData
                {
                    Score = (float)p.Score,
                    EngagementRate = p.EngagementRate,
                    AttendanceCount = p.AttendanceCount,
                    StudyHours = p.StudyHours
                }).ToList();

            if (data.Count < 10)
                return;

            var trainingData = mlContext.Data.LoadFromEnumerable(data);

            var pipeline = mlContext.Transforms.Concatenate("Features",
                    nameof(BranchPerformanceData.EngagementRate),
                    nameof(BranchPerformanceData.AttendanceCount),
                    nameof(BranchPerformanceData.StudyHours))
                .Append(mlContext.Regression.Trainers.Sdca(labelColumnName: "Score"));

            var model = pipeline.Fit(trainingData);

            Directory.CreateDirectory("MLModels"); // ← تم تغييره من wwwroot/Models
            mlContext.Model.Save(model, trainingData.Schema, ModelPath);
        }
    }
}
