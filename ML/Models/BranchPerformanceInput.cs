using Microsoft.ML.Data;

public class BranchPerformanceInput
{
    [LoadColumn(0)] public float TotalStudents { get; set; }
    [LoadColumn(1)] public float TotalCourses { get; set; }
    [LoadColumn(2)] public float TotalProjects { get; set; }
    [LoadColumn(3)] public float AverageEngagement { get; set; }
    [LoadColumn(4)] public float AverageAttendance { get; set; }

    // المطلوب التنبؤ به
    [LoadColumn(5), ColumnName("Label")] public float AveragePerformance { get; set; }
}
