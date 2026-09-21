using Microsoft.ML.Data;

namespace QdratNew.Entities
{
    public class CoursePredictionModel
    {
        [LoadColumn(0)] public float Month { get; set; }  // ✅ الشهر
        [LoadColumn(1)] public float Registrations { get; set; }  // ✅ عدد التسجيلات
    }

    public class CoursePrediction
    {
        [ColumnName("Score")]
        public float PredictedRegistrations { get; set; }  // ✅ التوقع لعدد التسجيلات القادمة
    }
}
