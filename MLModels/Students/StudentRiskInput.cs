using Microsoft.ML.Data;

public class StudentRiskInput
{
    [LoadColumn(0)] public float StudyHours { get; set; }
    [LoadColumn(1)] public float ExercisesCompleted { get; set; }
    [LoadColumn(2)] public float AttendanceCount { get; set; }
    [LoadColumn(3)] public float EngagementRate { get; set; }

    [LoadColumn(4), ColumnName("Label")]
    public float Score { get; set; }  // المطلوب توقعه
}

public class StudentRiskPrediction
{
    [ColumnName("Score")]
    public float PredictedScore { get; set; }
}
