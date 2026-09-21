using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Analytics.Interfaces;
using QdratNew.ViewModels.Analytics;

namespace QdratNew.Services.Analytics.Implementation
{
    public class WeakPointEngine : IWeakPointEngine
    {
        private readonly ApplicationDbContext _context;

        public WeakPointEngine(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // 🔴 Weak Questions
        // =========================================================
        public async Task<List<WeakPointVM>> GetWeakQuestionsAsync(WeakPointFilter filter)
        {
            // ===============================
            // 1️⃣ تحميل البيانات (بدون شروط SQL)
            // ===============================
            var attempts = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                    .ThenInclude(q => q.Lesson)
                .AsNoTracking()
                .ToListAsync();

            var enrollments = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .ToListAsync();

            // ===============================
            // 2️⃣ فلترة في الذاكرة (SQL 2014 SAFE)
            // ===============================
            var filtered = new List<QuestionAttemptNew>();

            foreach (var a in attempts)
            {
                bool isValid = true;

                if (filter.BatchId.HasValue)
                {
                    bool studentInBatch = false;

                    foreach (var e in enrollments)
                    {
                        if (e.StudentID == a.StudentId &&
                            e.BatchId == filter.BatchId.Value)
                        {
                            studentInBatch = true;
                            break;
                        }
                    }

                    if (!studentInBatch)
                        isValid = false;
                }

                if (isValid)
                    filtered.Add(a);
            }

            // ===============================
            // 3️⃣ تحليل متقدم (Decision Engine)
            // ===============================
            var result = filtered
                .GroupBy(a => a.QuestionId)
                .Select(g =>
                {
                    var total = g.Count();
                    var wrong = g.Count(x => !x.IsCorrect);
                    var avgTime = g.Average(x => x.TimeTakenSeconds);

                    var q = g.First().Question;

                    // =========================
                    // Accuracy
                    // =========================
                    var accuracy = total == 0
                        ? 0
                        : ((total - wrong) * 100.0) / total;

                    // =========================
                    // Penalties
                    // =========================
                    double timePenalty = 0;
                    if (avgTime > 60) timePenalty = 10;
                    else if (avgTime < 5) timePenalty = 15;

                    double attemptPenalty = total > 5 ? 10 : 0;

                    // =========================
                    // Final Score
                    // =========================
                    var score = accuracy - timePenalty - attemptPenalty;

                    if (score < 0) score = 0;
                    if (score > 100) score = 100;

                    return new WeakPointVM
                    {
                        QuestionId = q.Id,
                        QuestionTitle = q.Title,

                        LessonId = q.LessonId,
                        LessonName = q.Lesson.Title,

                        TotalAttempts = total,
                        WrongAnswers = wrong,

                        // 🔥 بدل النسبة القديمة
                        WrongPercentage = Math.Round(100 - score, 1)
                    };
                })
                .OrderByDescending(x => x.WrongPercentage)
                .Take(20)
                .ToList();

            return result;
        }

        // =========================================================
        // 🔴 Weak Lessons (الأهم)
        // =========================================================
        public async Task<List<WeakLessonVM>> GetWeakLessonsAsync(WeakPointFilter filter)
        {
            // ===============================
            // 1️⃣ تحميل البيانات
            // ===============================
            var attempts = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                    .ThenInclude(q => q.Lesson)
                .AsNoTracking()
                .ToListAsync();

            var enrollments = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .ToListAsync();

            // ===============================
            // 2️⃣ فلترة في الذاكرة
            // ===============================
            var filtered = new List<QuestionAttemptNew>();

            foreach (var a in attempts)
            {
                bool isValid = true;

                if (filter.BatchId.HasValue)
                {
                    bool studentInBatch = false;

                    foreach (var e in enrollments)
                    {
                        if (e.StudentID == a.StudentId &&
                            e.BatchId == filter.BatchId.Value)
                        {
                            studentInBatch = true;
                            break;
                        }
                    }

                    if (!studentInBatch)
                        isValid = false;
                }

                if (isValid)
                    filtered.Add(a);
            }

            // ===============================
            // 3️⃣ تحليل متقدم للدروس
            // ===============================
            var result = filtered
                .GroupBy(a => a.Question.LessonId)
                .Select(g =>
                {
                    var total = g.Count();
                    var wrong = g.Count(x => !x.IsCorrect);
                    var avgTime = g.Average(x => x.TimeTakenSeconds);

                    var lesson = g.First().Question.Lesson;

                    var accuracy = total == 0
                        ? 0
                        : ((total - wrong) * 100.0) / total;

                    double timePenalty = 0;
                    if (avgTime > 60) timePenalty = 10;
                    else if (avgTime < 5) timePenalty = 15;

                    double attemptPenalty = total > 5 ? 10 : 0;

                    var score = accuracy - timePenalty - attemptPenalty;

                    if (score < 0) score = 0;
                    if (score > 100) score = 100;

                    return new WeakLessonVM
                    {
                        LessonId = lesson.Id,
                        LessonName = lesson.Title,

                        TotalAttempts = total,
                        WrongAnswers = wrong,

                        // 🔥 المؤشر الحقيقي
                        WrongPercentage = Math.Round(100 - score, 1)
                    };
                })
                .OrderByDescending(x => x.WrongPercentage)
                .Take(10)
                .ToList();

            return result;
        }
    }
}