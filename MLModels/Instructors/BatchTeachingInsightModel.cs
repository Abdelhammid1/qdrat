using Microsoft.ML.Data;

namespace QdratNew.MLModels.Instructors
{
    public class BatchTeachingInsightInput
    {
        [LoadColumn(0)] public float AvgAttendance { get; set; }
        [LoadColumn(1)] public float AvgHomeworkScore { get; set; }
        [LoadColumn(2)] public float HomeworkCompletionRate { get; set; }
        [LoadColumn(3)] public float AvgTestScore { get; set; }
        [LoadColumn(4)] public float LessonsCompletionRate { get; set; }
        [LoadColumn(5)] public float StudentsAtRiskCount { get; set; }

        [LoadColumn(6), ColumnName("Label")]
        public string Recommendation { get; set; }

       

    }

    public class BatchTeachingInsightPrediction
    {
        [ColumnName("PredictedLabel")]
        public string Recommendation { get; set; }
    }
}
