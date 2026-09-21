using Microsoft.ML;
using QdratNew.MLModels.Instructors;
using System.Collections.Generic;
using System.IO;

namespace QdratNew.Services.AI
{
    public static class TeachingInsightTrainer
    {
        public static string Train()
        {
            var data = new List<BatchTeachingInsightInput>
            {
                new() {
                    AvgAttendance = 90, AvgHomeworkScore = 85, HomeworkCompletionRate = 88,
                    AvgTestScore = 87, LessonsCompletionRate = 90, StudentsAtRiskCount = 0,
                    Recommendation = "📈 الأداء ممتاز، استمر بنفس الخطة."
                },
                new() {
                    AvgAttendance = 70, AvgHomeworkScore = 65, HomeworkCompletionRate = 60,
                    AvgTestScore = 62, LessonsCompletionRate = 70, StudentsAtRiskCount = 2,
                    Recommendation = "⚠ تحتاج إلى مراجعة الطلاب الضعفاء قبل إكمال المحور القادم."
                },
                new() {
                    AvgAttendance = 50, AvgHomeworkScore = 40, HomeworkCompletionRate = 35,
                    AvgTestScore = 45, LessonsCompletionRate = 50, StudentsAtRiskCount = 5,
                    Recommendation = "🚨 الدفعة في خطر، يجب عقد جلسة علاجية جماعية قبل الاستمرار."
                },
                new() {
                    AvgAttendance = 80, AvgHomeworkScore = 75, HomeworkCompletionRate = 80,
                    AvgTestScore = 78, LessonsCompletionRate = 85, StudentsAtRiskCount = 1,
                    Recommendation = "✅ استمر، وقم بتكثيف المتابعة للطالب المتأخر الوحيد."
                },
            };

            var mlContext = new MLContext();
            var trainingData = mlContext.Data.LoadFromEnumerable(data);

            var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label")
                .Append(mlContext.Transforms.Concatenate("Features",
                    nameof(BatchTeachingInsightInput.AvgAttendance),
                    nameof(BatchTeachingInsightInput.AvgHomeworkScore),
                    nameof(BatchTeachingInsightInput.HomeworkCompletionRate),
                    nameof(BatchTeachingInsightInput.AvgTestScore),
                    nameof(BatchTeachingInsightInput.LessonsCompletionRate),
                    nameof(BatchTeachingInsightInput.StudentsAtRiskCount)
                ))
                .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var model = pipeline.Fit(trainingData);

            var modelPath = "MLModels/Instructors/BatchTeachingInsightModel.zip";
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            mlContext.Model.Save(model, trainingData.Schema, modelPath);

            return "✅ تم تدريب النموذج بنجاح وحفظه في: " + modelPath;
        }
    }
}
