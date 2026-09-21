using Microsoft.ML.Data;

namespace QdratNew.AI.MLModels.Students
{
    public class StudentPerformanceData
    {
        [LoadColumn(0)] public float PreviousScore { get; set; }
        [LoadColumn(1)] public float StudyHours { get; set; }
        [LoadColumn(2)] public float ExercisesCompleted { get; set; }
        [LoadColumn(3)] public float AttendanceCount { get; set; }
        [LoadColumn(4)] public float EngagementRate { get; set; }

        // ✅ العمود اللي هنتنبأ به
        [LoadColumn(5), ColumnName("Label")]
        public float Score { get; set; }
    }

}
