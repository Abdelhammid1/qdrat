using Microsoft.ML;
using QdratNew.MLModels.StudentProgressModel;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.AI
{
    public class AIStudentProgressTrainer
    {
        private readonly IAIStudentProgressTrainingService _dataService;

        public AIStudentProgressTrainer(IAIStudentProgressTrainingService dataService)
        {
            _dataService = dataService;
        }

        public async Task<string> TrainAndSaveModelAsync()
        {
            var context = new MLContext();

            var trainingData = await _dataService.GetTrainingDataAsync();
            var distinctLabels = trainingData.Select(d => d.RecommendationText).Distinct().ToList();

            if (distinctLabels.Count < 2)
                throw new InvalidOperationException("❌ لا يمكن تدريب النموذج: يجب أن تحتوي البيانات على توصيتين مختلفتين على الأقل.");

            var dataView = context.Data.LoadFromEnumerable(trainingData);

            var pipeline = context.Transforms.Conversion.MapValueToKey("Label", nameof(AIStudentProgressTrainingData.RecommendationText))
                .Append(context.Transforms.Concatenate("Features",
                    nameof(AIStudentProgressTrainingData.CompletedLessons),
                    nameof(AIStudentProgressTrainingData.CompletedExercises),
                    nameof(AIStudentProgressTrainingData.ProgressPercentage),
                    nameof(AIStudentProgressTrainingData.Score),
                    nameof(AIStudentProgressTrainingData.DifficultyLevelEncoded)))
                .Append(context.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: "Label", featureColumnName: "Features"))
                .Append(context.Transforms.Conversion.MapKeyToValue("PredictedLabel", "Label"));

            var model = pipeline.Fit(dataView);

            var modelPath = Path.Combine("MLModels", "StudentProgress", "AIStudentProgressModel.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            context.Model.Save(model, dataView.Schema, modelPath);

            return modelPath;
        }
    }
}
