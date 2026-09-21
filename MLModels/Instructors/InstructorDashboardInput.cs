using Microsoft.ML.Data;

namespace QdratNew.MLModels.Instructors
{
    public class InstructorDashboardInput
    {
        [LoadColumn(0)] public float AvgAttendance { get; set; }
        [LoadColumn(1)] public float AvgHomeworkScore { get; set; }
        [LoadColumn(2)] public float LessonsCompletionRate { get; set; }
        [LoadColumn(3)] public float StudentSuccessRate { get; set; }
        [LoadColumn(4), ColumnName("Label")] public string Recommendation { get; set; }
    }

    public class InstructorDashboardPrediction
    {
        [ColumnName("PredictedLabel")]
        public string Recommendation { get; set; }
    }
}
