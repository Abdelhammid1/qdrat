using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Homework.Interfaces;
using QdratNew.ViewModels.Homework;
using System.Text.Json;

namespace QdratNew.Services.Homework.Implementations
{
    public class HomeworkAnalyticsService : IHomeworkAnalyticsService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public HomeworkAnalyticsService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // 📈 Progress Timeline (Hybrid)
        // =========================================================
        public async Task<List<HomeworkProgressPoint>> GetStudentProgressAsync(int studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var analytics = await db.StudentHomeworkAnalytics
                .Where(x => x.StudentId == studentId)
                .OrderBy(x => x.HomeworkSetId)
                .ToListAsync();

            var result = new List<HomeworkProgressPoint>();

            foreach (var a in analytics)
            {
                result.Add(new HomeworkProgressPoint
                {
                    HomeworkTitle = "واجب " + a.HomeworkSetId,
                    Score = a.Score
                });
            }

            // fallback للقديم
            if (!result.Any())
            {
                var old = await db.HomeworkSetStudents
                    .Where(x => x.StudentId == studentId && x.Score.HasValue)
                    .OrderBy(x => x.HomeworkSetId)
                    .ToListAsync();

                foreach (var h in old)
                {
                    result.Add(new HomeworkProgressPoint
                    {
                        HomeworkTitle = "واجب " + h.HomeworkSetId,
                        Score = h.Score.Value
                    });
                }
            }

            return result;
        }

        // =========================================================
        // 🧠 Behavior Analysis
        // =========================================================
        public (StudentBehaviorLevel level, string text) AnalyzeBehavior(
            double avgTime,
            int correct,
            int total)
        {
            double accuracy = total == 0 ? 0 : (correct * 100.0 / total);

            if (avgTime <= 0)
            {
                return (
                    StudentBehaviorLevel.طبيعي,
                    "لم يتم تسجيل زمن الحل بشكل كافٍ، لذلك تم تقييم السلوك اعتمادًا على النتيجة وعدد الإجابات."
                );
            }

            if (avgTime < 5 && accuracy > 80)
            {
                return (
                    StudentBehaviorLevel.غش_محتمل,
                    "حل سريع جدًا مع دقة عالية، قد يشير إلى مساعدة خارجية."
                );
            }

            if (avgTime < 7 && accuracy > 60)
            {
                return (
                    StudentBehaviorLevel.مريب,
                    "سرعة الحل أعلى من الطبيعي، يُفضل المتابعة."
                );
            }

            if (avgTime >= 7 && accuracy >= 70)
            {
                return (
                    StudentBehaviorLevel.ممتاز,
                    "أداء متوازن يعكس فهم جيد."
                );
            }

            return (
                StudentBehaviorLevel.طبيعي,
                "أداء طبيعي يحتاج إلى مزيد من التدريب."
            );
        }

        // =========================================================
        // 👨‍👩‍👦 Parent Report + Recommendations
        // =========================================================
        public ParentReportResult BuildParentReport(
            double score,
            int correct,
            int wrong,
            int skipped,
            double avgTime,
            StudentBehaviorLevel behavior,
            string progress)
        {
            var result = new ParentReportResult();

            result.Level = score switch
            {
                >= 85 => "ممتاز",
                >= 70 => "جيد جدًا",
                >= 50 => "متوسط",
                _ => "ضعيف"
            };

            result.Commitment = behavior switch
            {
                StudentBehaviorLevel.ممتاز => "✅ ملتزم ومنضبط",
                StudentBehaviorLevel.طبيعي => "✔ التزام متوسط",
                StudentBehaviorLevel.مريب => "⚠️ يحتاج متابعة",
                StudentBehaviorLevel.غش_محتمل => "❌ غير موثوق",
                _ => "غير محدد"
            };

            result.Progress = progress;

            var rec = new List<string>();

            if (skipped > 0)
                rec.Add($"مراجعة الأسئلة المتروكة وعددها {skipped} حتى لا يفقد الطالب درجات سهلة.");

            if (wrong > 0)
                rec.Add($"مراجعة الأسئلة الخاطئة وعددها {wrong} مع توضيح سبب اختيار الإجابة الصحيحة.");

            if (wrong > correct && correct > 0)
                rec.Add("التركيز على فهم الأساسيات قبل الانتقال لتدريبات أصعب.");

            if (avgTime > 0 && avgTime < 5)
                rec.Add("تدريب الطالب على التمهل وقراءة السؤال كاملًا قبل الإجابة.");

            if (score < 60)
                rec.Add("إعادة مذاكرة المؤشرات الضعيفة ثم حل تدريب قصير عليها.");

            if (score >= 80)
                rec.Add("الاستمرار بنفس الأداء مع تدريب إضافي خفيف للحفاظ على المستوى.");

            if (!rec.Any())
                rec.Add("المتابعة المنتظمة وحل واجبات قصيرة للحفاظ على ثبات المستوى.");

            result.HomeRecommendations = rec;

            result.ParentInsight = GenerateParentInsight(
                score, behavior, avgTime, skipped, wrong
            );

            return result;
        }

        private string GenerateParentInsight(
            double score,
            StudentBehaviorLevel behavior,
            double avgTime,
            int skipped,
            int wrong)
        {
            if (behavior == StudentBehaviorLevel.غش_محتمل)
                return "يوجد سلوك غير طبيعي، يُفضل المتابعة المباشرة.";

            if (avgTime > 0 && avgTime < 5)
                return "الطالب يحل بسرعة قد تؤثر على الفهم.";

            if (score < 50)
                return "المستوى منخفض ويحتاج دعمًا ومراجعة للمؤشرات الأساسية.";

            if (wrong > skipped)
                return "الطالب يحاول الحل، لكنه يحتاج مراجعة للأخطاء لتحسين الفهم.";

            if (score >= 80)
                return "أداء ممتاز ومستقر.";

            return "أداء مقبول مع فرصة للتحسن.";
        }

        // =========================================================
        // 🔥 Build Analytics (Hybrid)
        // =========================================================
        public async Task BuildAndStoreAsync(int studentId, int homeworkSetId)
        {
            using var db = _contextFactory.CreateDbContext();

            var existing = await db.StudentHomeworkAnalytics
                .FirstOrDefaultAsync(x =>
                    x.StudentId == studentId &&
                    x.HomeworkSetId == homeworkSetId);

            var questions = await db.Homeworks
                .Where(h => h.StudentId == studentId && h.HomeworkSetId == homeworkSetId)
                .Select(h => h.QuestionId)
                .Distinct()
                .ToListAsync();

            int totalQuestions = questions.Count;
            if (totalQuestions == 0) return;

            var attempts = await db.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.HomeworkSetId == homeworkSetId)
                .ToListAsync();

            var finalAttempts = attempts
                .GroupBy(x => x.QuestionId)
                .Select(g => g.OrderByDescending(x => x.AttemptedAt).First())
                .ToList();

            int correct = finalAttempts.Count(x => x.IsCorrect);
            int wrong = finalAttempts.Count(x => !x.IsCorrect && !string.IsNullOrEmpty(x.SelectedAnswer));
            int skipped = totalQuestions - finalAttempts.Count(x => !string.IsNullOrEmpty(x.SelectedAnswer));

            var validTimes = finalAttempts.Where(x => x.TimeTakenSeconds > 0).Select(x => x.TimeTakenSeconds).ToList();

            double avgTime = validTimes.Any() ? validTimes.Average() : 0;
            double totalSeconds = validTimes.Sum();

            double score = totalQuestions > 0
                ? Math.Round(correct * 100.0 / totalQuestions, 1)
                : 0;

            // fallback القديم
            if (!finalAttempts.Any())
            {
                var oldScore = await db.HomeworkSetStudents
                    .Where(x => x.StudentId == studentId && x.HomeworkSetId == homeworkSetId)
                    .Select(x => x.Score)
                    .FirstOrDefaultAsync();

                if (oldScore.HasValue)
                    score = oldScore.Value;
            }

            var behavior = AnalyzeBehavior(avgTime, correct, totalQuestions);

            StudentHomeworkAnalytics entity;

            if (existing != null)
                entity = existing;
            else
            {
                entity = new StudentHomeworkAnalytics
                {
                    StudentId = studentId,
                    HomeworkSetId = homeworkSetId
                };
                db.StudentHomeworkAnalytics.Add(entity);
            }

            entity.Score = score;
            entity.BehaviorLevel = (int)behavior.level;
            entity.TotalQuestions = totalQuestions;
            entity.CorrectAnswers = correct;
            entity.WrongAnswers = wrong;
            entity.SkippedAnswers = skipped;
            entity.AvgTimePerQuestion = Math.Round(avgTime, 1);
            entity.TotalTimeSeconds = totalSeconds;

            await db.SaveChangesAsync();
        }
    }
}
