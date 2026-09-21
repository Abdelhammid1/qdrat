using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.MLModels.StudentProgressModel;
using QdratNew.Services.Interfaces;
using StudentProgressEntity = QdratNew.Entities.StudentProgress;

namespace QdratNew.Services.AI
{
    public class AIStudentProgressTrainingService : IAIStudentProgressTrainingService
    {
        private readonly ApplicationDbContext _context;

        public AIStudentProgressTrainingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<AIStudentProgressTrainingData>> GetTrainingDataAsync()
        {
            var data = await _context.StudentProgress
                .Where(p => p.StudentID != 17) // استبعاد الطالب رقم 17 من التدريب
                .ToListAsync();

            return data.Select(p => new AIStudentProgressTrainingData
            {
                CompletedLessons = p.CompletedLessons,
                CompletedExercises = p.CompletedExercises,
                ProgressPercentage = (float)p.ProgressPercentage,
                Score = (float)p.Score,
                DifficultyLevelEncoded = MapDifficulty(p.DifficultyLevel),
                RecommendationText = GenerateRecommendation(p)
            }).ToList();
        }

        private float MapDifficulty(string level)
        {
            return level switch
            {
                "سهل" => 1f,
                "متوسط" => 2f,
                "صعب" => 3f,
                _ => 2f
            };
        }

        private string GenerateRecommendation(StudentProgressEntity p)
        {
            if (p.Score < 40 && p.DifficultyLevel == "صعب")
                return "📉 أداء ضعيف مع صعوبة مرتفعة. ننصح بمراجعة المحاور السابقة والتركيز على التمارين.";

            if (p.Score >= 40 && p.Score < 70 && p.ProgressPercentage < 50)
                return "📌 مستواك متوسط، لكن التقدم بطيء. حاول زيادة التفاعل مع الدروس والواجبات.";

            if (p.Score >= 70 && p.CompletedLessons >= 5 && p.CompletedExercises >= 10)
                return "🚀 تقدم ملحوظ. استمر بهذه الوتيرة الممتازة.";

            if (p.ProgressPercentage >= 85 && p.Score >= 85)
                return "🌟 أنت من الطلاب المتميزين. ننصحك بخوض تحديات إضافية لتعزيز المهارات.";

            return "🔁 استمر، وحاول تحسين أدائك في الدروس القادمة.";
        }

    }
}
