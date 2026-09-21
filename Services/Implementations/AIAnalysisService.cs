using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Students;

namespace QdratNew.Services.Implementations
{
    public class AIAnalysisService : IAIAnalysisService
    {
        private readonly ApplicationDbContext _context;

        public AIAnalysisService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<StudentAIAnalysisResultViewModel?> GetAnalysisForStudentAsync(int studentId)
        {
            var logs = await _context.StudentActivityLogs
                .Where(l => l.StudentId == studentId)
                .ToListAsync();

            if (!logs.Any())
                return null;

            var assignments = logs.Where(l => l.ActivityType == "Homework").ToList();
            var exams = logs.Where(l => l.ActivityType == "Exam").ToList();
            var correctAnswers = logs.Count(l => l.WasCorrect == true);
            var totalAnswers = logs.Count(l => l.WasCorrect.HasValue);
            var totalSessions = logs.Count();
            var completedSessions = logs.Count(l => l.Score.HasValue);
            var totalTime = assignments.Sum(a => a.Score.HasValue ? a.Score.Value : 0);
            var avgTime = assignments.Any() ? totalTime / assignments.Count : 0;

            var viewModel = new StudentAIAnalysisResultViewModel
            {
                AnalyzedAt = DateTime.Now,
                SuccessRate = (float)((correctAnswers * 100.0) / totalAnswers),
                SessionCompletionRatio = (float)((completedSessions * 100.0) / totalSessions),
                AverageHomeworkTime = avgTime,
                TotalAssignments = assignments.Count,
                TotalExams = exams.Count,
                AssessedLevel = "مقبول", // مبدئيًا
                RiskScore = (float)(100 - ((correctAnswers * 100.0) / totalAnswers)),
                Recommendations = GenerateRecommendations(correctAnswers, totalAnswers)
            };

            return viewModel;
        }

        private List<string> GenerateRecommendations(int correct, int total)
        {
            var recs = new List<string>();

            if (total == 0)
            {
                recs.Add("لم تقم بأي واجب أو اختبار حتى الآن. يرجى البدء بالتفاعل مع النظام.");
            }
            else
            {
                var accuracy = (correct * 100.0) / total;

                if (accuracy < 50)
                    recs.Add("يرجى مراجعة الدروس السابقة بسبب انخفاض نسبة الإجابات الصحيحة.");
                else if (accuracy < 75)
                    recs.Add("أداءك متوسط، حاول تحسين مستواك في المؤشرات الأضعف.");
                else
                    recs.Add("أداء ممتاز، استمر على هذا النحو.");

                if (total < 5)
                    recs.Add("قم بحل المزيد من الواجبات لتحسين تحليل الذكاء الاصطناعي.");
            }

            return recs;
        }
    }
}
