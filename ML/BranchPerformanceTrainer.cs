using Microsoft.ML;
using QdratNew.AI.MLModels.Branches;
using System.Collections.Generic;

public static class BranchPerformanceTrainer
{
    private static readonly string ModelPath = "MLModels/BranchPerformanceModel.zip";
    private static MLContext _mlContext = new();

    public static void TrainModel(IEnumerable<BranchPerformanceInput> trainingData)
    {
        IDataView data = _mlContext.Data.LoadFromEnumerable(trainingData);

        var pipeline = _mlContext.Transforms.Concatenate("Features",
                nameof(BranchPerformanceInput.TotalStudents),
                nameof(BranchPerformanceInput.TotalCourses),
                nameof(BranchPerformanceInput.TotalProjects),
                nameof(BranchPerformanceInput.AverageEngagement),
                nameof(BranchPerformanceInput.AverageAttendance))
            .Append(_mlContext.Regression.Trainers.Sdca());

        var model = pipeline.Fit(data);
        _mlContext.Model.Save(model, data.Schema, ModelPath);
    }

    public static float Predict(BranchPerformanceInput input)
    {
        ITransformer model = _mlContext.Model.Load(ModelPath, out _);
        var predictionEngine = _mlContext.Model.CreatePredictionEngine<BranchPerformanceInput, BranchPerformancePrediction>(model);
        return predictionEngine.Predict(input).Score;
    }
}
