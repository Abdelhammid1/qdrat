using Microsoft.ML.Data;

namespace QdratNew.MLModels.StudentProgressModel
{
    public class AIStudentProgressTrainingData
    {
        [LoadColumn(0)]
        public float CompletedLessons { get; set; }

        [LoadColumn(1)]
        public float CompletedExercises { get; set; }

        [LoadColumn(2)]
        public float ProgressPercentage { get; set; }

        [LoadColumn(3)]
        public float Score { get; set; }

        [LoadColumn(4)]
        public float DifficultyLevelEncoded { get; set; }

        [LoadColumn(5)]
        public string RecommendationText { get; set; } = string.Empty;
    }
}
