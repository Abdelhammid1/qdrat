using Microsoft.ML;
using QdratNew.MLModels.StudentProgressModel;

public class AIStudentProgressPredictionService
{
    private readonly string _modelPath;

    public AIStudentProgressPredictionService()
    {
        var projectRoot = Directory.GetCurrentDirectory();
        _modelPath = Path.Combine(projectRoot, "MLModels", "StudentProgress", "AIStudentProgressModel.zip");
    }

    public string Predict(AIStudentProgressInput input)
    {
        var context = new MLContext();
        ITransformer trainedModel = context.Model.Load(_modelPath, out _);

        var predictionEngine = context.Model
            .CreatePredictionEngine<AIStudentProgressInput, AIStudentProgressOutput>(trainedModel);

        var result = predictionEngine.Predict(input);

        System.Diagnostics.Debug.WriteLine("📌 التوصية المستخرجة من النموذج: " + result.RecommendationText);

        return result.RecommendationText;
    }


}
