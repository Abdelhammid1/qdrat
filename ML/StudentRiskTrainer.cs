using Microsoft.ML;
using QdratNew.AI.MLModels.Students;
using QdratNew.Data;

public static class StudentRiskTrainer
{
    private static readonly string ModelPath = "MLModels/StudentRiskModel.zip";

    public static string Train(ApplicationDbContext context)
    {
        var mlContext = new MLContext();

        var data = context.StudentPerformances
            .Select(sp => new StudentRiskInput
            {
                StudyHours = sp.StudyHours,
                ExercisesCompleted = sp.ExercisesCompleted,
                AttendanceCount = sp.AttendanceCount,
                EngagementRate = sp.EngagementRate,
                Score = (float)sp.Score
            })
            .ToList();

        if (!data.Any())
            return "❌ لا توجد بيانات كافية لتدريب النموذج.";

        var dataView = mlContext.Data.LoadFromEnumerable(data);

        var pipeline = mlContext.Transforms.Concatenate("Features",
                    nameof(StudentRiskInput.StudyHours),
                    nameof(StudentRiskInput.ExercisesCompleted),
                    nameof(StudentRiskInput.AttendanceCount),
                    nameof(StudentRiskInput.EngagementRate))
                .Append(mlContext.Regression.Trainers.Sdca());

        var model = pipeline.Fit(dataView);

        mlContext.Model.Save(model, dataView.Schema, ModelPath);
        return "✅ تم تدريب نموذج الخطر الأكاديمي وحفظه بنجاح.";
    }

    public static float Predict(StudentRiskInput input)
    {
        var mlContext = new MLContext();
        var model = mlContext.Model.Load(ModelPath, out _);
        var engine = mlContext.Model.CreatePredictionEngine<StudentRiskInput, StudentRiskPrediction>(model);
        return engine.Predict(input).PredictedScore;
    }
}
