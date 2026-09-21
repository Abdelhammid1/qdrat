using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Analytics;

namespace QdratNew.Services.Implementations
{
    public class PerformanceInsightService : IPerformanceInsightService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public PerformanceInsightService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ============================================================
        //   🔥 التحليل الشامل للطالب
        // ============================================================
        public async Task<PerformanceInsightResult> AnalyzeStudentPerformanceAsync(int studentId, int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var results = await _context.StudentIndicatorResults
                .Include(r => r.Section)
                .Include(r => r.PerformanceIndicatorExam)
                .ThenInclude(e => e.Batch)
                .Where(r => r.StudentId == studentId && r.PerformanceIndicatorExamId == examId)
                .ToListAsync();

            if (!results.Any())
                return null;

            var exam = results.First().PerformanceIndicatorExam;

            // 🟢 النتيجة العامة
            double overallScore = results.Average(r => r.ScorePercent);

            // 🟡 محاولات الأسئلة
            var attempts = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                .ThenInclude(q => q.Lesson)
                .Where(a => a.StudentId == studentId && a.PerformanceIndicatorExamId == examId)
                .ToListAsync();

            double totalSeconds = attempts.Sum(a => a.TimeTakenSeconds);
            double elapsedMinutes = Math.Round(totalSeconds / 60.0, 2);
            string formattedTime = TimeSpan.FromSeconds(totalSeconds).ToString(@"hh\:mm\:ss");

            // معادلة الزمن الجديد
            int examTotalSeconds = exam.DurationMinutes * 60;
            double timeUsagePercent = examTotalSeconds == 0
                ? 0
                : Math.Round((totalSeconds / examTotalSeconds) * 100, 1);

            // عدد المتخطّاة
            int answered = attempts.Count(a => !string.IsNullOrEmpty(a.SelectedAnswer));
            int totalQuestions = await _context.PerformanceIndicatorExamQuestions
                .CountAsync(eq => eq.PerformanceIndicatorExamId == examId);
            int skipped = totalQuestions - answered;

            // 🟦 تحليل المحاور
            var sections = new List<SectionInsight>();

            foreach (var r in results)
            {
                var secAttempts = attempts
                    .Where(a =>
                        a.Question != null &&
                        a.Question.Lesson != null &&
                        a.Question.Lesson.SectionId == r.SectionId)
                    .ToList();

                int correct = secAttempts.Count(a => a.IsCorrect);
                int wrong = secAttempts.Count(a =>
                    !a.IsCorrect &&
                    !string.IsNullOrWhiteSpace(a.SelectedAnswer));
                int skippedSec = secAttempts.Count(a =>
                    string.IsNullOrWhiteSpace(a.SelectedAnswer));

                sections.Add(new SectionInsight
                {
                    SectionId = r.SectionId,
                    SectionTitle = r.Section.Title,
                    Score = r.ScorePercent,
                    CorrectCount = correct,
                    WrongCount = wrong,
                    SkippedCount = skippedSec,
                    Guidance = GetSectionGuidance(r.ScorePercent)
                });
            }

            // 🧠 السرعة × الدقة
            string speedAccuracy = GetSpeedAccuracyFeedback(timeUsagePercent, overallScore);

            // 🚨 كشف الغش Smart Detection
            string cheatingWarning = "";
            if (skipped > totalQuestions * 0.5 && timeUsagePercent < 30)
            {
                cheatingWarning =
                    "⚠️ عدد كبير من الأسئلة غير مُجاب عليها مع وقت قليل جدًا — يبدو أن الاختبار أُغلق مبكرًا أو لم يتم إكماله.";
            }

            return new PerformanceInsightResult
            {
                StudentId = studentId,
                ExamId = examId,
                OverallScore = overallScore,
                TimeUsagePercent = timeUsagePercent,
                SpeedAccuracyFeedback = speedAccuracy,
                ScoreBand = GetScoreBandDescription(overallScore),
                MotivationalMessage = GetMotivationalMessage(overallScore),
                Sections = sections,
                BatchName = exam.Batch?.Name ?? "—",
                ElapsedMinutes = elapsedMinutes,
                ElapsedTimeFormatted = formattedTime,
                SkippedQuestions = skipped,
                CheatingFlagMessage = cheatingWarning
            };
        }

        // ============================================================
        //   🎯 شرائح النتيجة العامة (Score Bands)
        // ============================================================
        public string GetScoreBandDescription(double score)
        {
            if (score < 60) return "أداء ضعيف (راسب) — يحتاج خطة علاجية";
            if (score < 70) return "مقبول لكن يحتاج تطوير (حل المزيد من التدريبات)";
            if (score < 80) return "جيد ولكن يحتاج تطوير للوصول لنسبة أعلى";
            if (score <= 90) return "أداء قوي، استمر لتصل إلى القمة";
            return "أداء مميز، أنت في القمة (حافظ على تميزك)";
        }

        // ============================================================
        //   🧠 السرعة × الدقة (15 حالة كاملة)
        // ============================================================
        public string GetSpeedAccuracyFeedback(double speedPercent, double scorePercent)
        {
            string result;

            // سرعة <40%
            if (speedPercent < 40)
            {
                if (scorePercent < 50) return "حل سريع جدًا مع دقة ضعيفة جدًا — راجع بدقة قبل التسليم.";
                if (scorePercent < 60) return "حل سريع ويحتاج تركيز أعلى.";
                if (scorePercent < 70) return "حل سريع جيد… ولكن تحتاج تثبيت الإجابات.";
                if (scorePercent < 80) return "حل سريع جيد مع دقة ممتازة.";
                return "حل سريع ودقيق جدًا — أداء ممتاز.";
            }

            // سرعة 40–70%
            if (speedPercent < 70)
            {
                if (scorePercent < 50) return "سرعة مناسبة لكن الدقة ضعيفة — راجع الأساسيات.";
                if (scorePercent < 60) return "سرعة مناسبة ودقة منخفضة — تحتاج تدريب إضافي.";
                if (scorePercent < 70) return "حل متوازن يحتاج تطوير.";
                if (scorePercent < 80) return "حل متوازن جيد.";
                return "توازن مثالي بين السرعة والدقة.";
            }

            // سرعة ≥70% → بطيء
            if (scorePercent < 50) result = "حل بطيء جدًا ودقة ضعيفة — درّب على إدارة الوقت.";
            else if (scorePercent < 60) result = "حل بطيء جدًا — حاول زيادة السرعة تدريجيًا.";
            else if (scorePercent < 70) result = "حل بطيء مع دقة متوسطة — جيد ولكن يحتاج سرعة.";
            else if (scorePercent < 80) result = "حل بطيء ودقة جيدة — قلل الزمن تدريجيًا.";
            else result = "حل بطيء ودقيق — أداء ممتاز.";

            // 🔥 Smart triggers 
            if (speedPercent < 20 && scorePercent >= 90)
                result += " 🟢 أداء استثنائي – سرعة ودقة عالية جدًا.";

            if (speedPercent < 30 && scorePercent < 60)
                result += " 🔴 تنبيه: قد يكون هناك حل عشوائي بسبب السرعة المنخفضة جدًا.";

            return result;
        }

        // ============================================================
        //   🧭 التوجيه العلاجي للمحاور
        // ============================================================
        public string GetSectionGuidance(double score)
        {
            if (score < 60) return "كنت مقصرًا في هذا المحور — تحتاج خطة علاجية قوية.";
            if (score < 70) return "في تقدم جيد — استمر في التدريب.";
            if (score < 80) return "أداء ممتاز — ركّز على التفاصيل الصغيرة.";
            if (score < 90) return "أداء قوي جدًا — حافظ على مستواك.";
            return "مستوى ممتاز جدًا — أداء احترافي!";
        }

        // ============================================================
        //   💬 التوصية العامة
        // ============================================================
        public string GetMotivationalMessage(double score)
        {
            if (score < 50) return "كل بداية فيها ضعف — المهم إنك تستمر.";
            if (score < 60) return "قليل من الجهد الإضافي يغير كل شيء.";
            if (score < 70) return "أنت تتقدم — استمر بثقة.";
            if (score < 80) return "عمل رائع — استمر بنفس العزيمة.";
            if (score < 90) return "مستوى قوي جدًا — تحية لالتزامك!";
            return "🔥 أداء خارق — أنت في القمة!";
        }
    }
}
