using System;
using System.Linq;
using QdratNew.Data;
using QdratNew.MLModels;
using Microsoft.EntityFrameworkCore;
using QdratNew.AI.Trainers;
namespace QdratNew.Services
{
    public class StudentAIAnalysisService
    {
        private readonly ApplicationDbContext _context;
        private readonly StudentPerformanceTrainer _predictionModel;

        public StudentAIAnalysisService(ApplicationDbContext context, StudentPerformanceTrainer predictionModel)
        {
            _context = context;
            _predictionModel = predictionModel;
        }

        public string AnalyzePerformanceLevel(int studentId)
        {
            if (studentId <= 0)
                return "❌ يجب تحديد رقم الطالب بشكل صحيح.";

            var studentPerformance = _context.StudentPerformances
                .Where(sp => sp.StudentID == studentId)
                .OrderByDescending(sp => sp.ExamDate)
                .FirstOrDefault();

            if (studentPerformance == null)
                return "⚠️ لا توجد بيانات تحليلية متاحة لهذا الطالب.";
            float previousScore = studentPerformance != null ? (float)studentPerformance.Score : 50.0f; // ✅ حل المشكلة

            float studyHours = _context.StudyPlans
                .Where(sp => sp.StudentID == studentId)
                .Sum(sp => (float?)sp.HoursPerWeek) ?? 10.0f;

            float exercisesCompleted = _context.StudentProgress
                .Where(sp => sp.StudentID == studentId)
                .Sum(sp => (float?)sp.CompletedExercises) ?? 20.0f;

            float attendanceCount = _context.StudentProgress
                .Where(sp => sp.StudentID == studentId)
                .Sum(sp => (float?)sp.CompletedLessons) ?? 15.0f;

            float engagementRate = _context.StudentProgress
     .Where(sp => sp.StudentID == studentId)
    .Select(sp => (float)sp.ProgressPercentage)
     .ToList()
     .DefaultIfEmpty(50.0f)
     .Average();



            // ✅ استدعاء التنبؤ بعد ضمان القيم
            float predictedScore = _predictionModel.PredictScore(
                previousScore,
                studyHours,
                exercisesCompleted,
                attendanceCount,
                engagementRate
            );


            return $"📊 التقييم: {predictedScore:F1}/100";
        }
    }
}
