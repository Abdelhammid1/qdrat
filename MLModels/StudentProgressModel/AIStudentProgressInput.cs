using Microsoft.ML.Data;

namespace QdratNew.MLModels.StudentProgressModel
{
    public class AIStudentProgressInput
    {
        public float Score { get; set; }
        public float CompletedLessons { get; set; }
        public float CompletedExercises { get; set; }
        public float ProgressPercentage { get; set; }
        public float DifficultyLevelEncoded { get; set; }
        public string RecommendationText { get; set; } = "";

    }
}
