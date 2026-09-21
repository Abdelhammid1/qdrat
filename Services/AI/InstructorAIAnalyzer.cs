using Microsoft.ML;
using QdratNew.MLModels.Instructors;
using System;
using System.IO;

namespace QdratNew.Services.AI
{
    public class InstructorAIAnalyzer
    {
        private readonly string _modelPath = "MLModels/Instructors/InstructorDashboardModel.zip";
        private readonly MLContext _mlContext;
        private readonly PredictionEngine<InstructorDashboardInput, InstructorDashboardPrediction>? _engine;

        public InstructorAIAnalyzer()
        {
            _mlContext = new MLContext();

            if (!File.Exists(_modelPath))
            {
                Console.WriteLine($"❌ ملف النموذج غير موجود: {_modelPath}");
                return;
            }

            try
            {
                var model = _mlContext.Model.Load(_modelPath, out _);
                _engine = _mlContext.Model.CreatePredictionEngine<InstructorDashboardInput, InstructorDashboardPrediction>(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ فشل تحميل النموذج: {ex.Message}");
            }
        }

        public string GetRecommendation(float attendance, float homework, float lessonsRate, float successRate)
        {
            if (_engine == null)
                return "❌ لم يتم تحميل النموذج. الرجاء إعادة التدريب.";

            var input = new InstructorDashboardInput
            {
                AvgAttendance = attendance,
                AvgHomeworkScore = homework,
                LessonsCompletionRate = lessonsRate,
                StudentSuccessRate = successRate
            };

            var prediction = _engine.Predict(input);
            return prediction.Recommendation ?? "⚠ لا توجد توصية واضحة من النموذج.";
        }
    }
}
