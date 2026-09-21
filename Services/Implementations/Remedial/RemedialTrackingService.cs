using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Remedial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations.Remedial
{
    /// <summary>
    /// ✅ إدارة التتبع والتقارير للجلسات العلاجية (Smart Tracking & AI Recommendations)
    /// </summary>
    public class RemedialTrackingService : IRemedialTrackingService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public RemedialTrackingService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ================================================================
        // 🟢 المرحلة الأولى: التخزين المؤقت داخل Session أثناء الجلسة
        // ================================================================

        public async Task CacheVideoProgressAsync(HttpContext http, int studentId, int sessionId, int videoId, int secondsWatched)
        {
            string key = $"RemedialProgress_{studentId}_{sessionId}_{videoId}";

            http.Session.SetInt32(key, secondsWatched);
            http.Session.SetString($"{key}_updated", DateTime.UtcNow.ToString("s"));

            await Task.CompletedTask;
        }

        public async Task CacheQuizAttemptAsync(HttpContext http, int studentId, int quizId, bool isCorrect)
        {
            string key = $"RemedialQuiz_{studentId}_{quizId}";
            http.Session.SetString(key, isCorrect ? "1" : "0");
            await Task.CompletedTask;
        }

        // ================================================================
        // 🟡 المرحلة الثانية: نقل بيانات الجلسة من الـSession إلى قاعدة البيانات
        // ================================================================
        public async Task FlushCachedProgressAsync(HttpContext http, int studentId, int sessionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // ✅ حفظ مشاهدات الفيديوهات
            var sessionKeys = http.Session.Keys
                .Where(k => k.StartsWith($"RemedialProgress_{studentId}_{sessionId}_"))
                .ToList();

            foreach (var key in sessionKeys)
            {
                var parts = key.Split('_');
                if (parts.Length < 4) continue;

                int videoId = int.Parse(parts[3]);
                int seconds = http.Session.GetInt32(key) ?? 0;

                var existing = await _context.RemedialSessionLogs
                    .FirstOrDefaultAsync(r => r.StudentId == studentId && r.SessionId == sessionId && r.VideoId == videoId);

                if (existing != null)
                {
                    existing.DurationWatched = seconds;
                    existing.WatchEnd = DateTime.UtcNow;
                    existing.IsCompleted = seconds > 60;
                    existing.UpdatedAt = DateTime.UtcNow;
                    _context.RemedialSessionLogs.Update(existing);
                }
                else
                {
                    _context.RemedialSessionLogs.Add(new RemedialSessionLog
                    {
                        StudentId = studentId,
                        SessionId = sessionId,
                        VideoId = videoId,
                        DurationWatched = seconds,
                        WatchStart = DateTime.UtcNow,
                        WatchEnd = DateTime.UtcNow,
                        IsCompleted = seconds > 60,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            // ✅ حفظ نتائج الاختبارات التطبيقية
            var quizKeys = http.Session.Keys
                .Where(k => k.StartsWith($"RemedialQuiz_{studentId}_"))
                .ToList();

            foreach (var key in quizKeys)
            {
                var parts = key.Split('_');
                if (parts.Length < 3) continue;

                int quizId = int.Parse(parts[2]);
                bool isCorrect = http.Session.GetString(key) == "1";

                var existingAttempt = await _context.StudentRemedialQuizResults
                    .FirstOrDefaultAsync(q => q.StudentId == studentId && q.RemedialQuizId == quizId);

                if (existingAttempt != null)
                {
                    existingAttempt.Score = isCorrect ? 100 : 0;
                    existingAttempt.Passed = isCorrect;
                    existingAttempt.TakenAt = DateTime.UtcNow;
                    _context.StudentRemedialQuizResults.Update(existingAttempt);
                }
                else
                {
                    _context.StudentRemedialQuizResults.Add(new StudentRemedialQuizResult
                    {
                        StudentId = studentId,
                        RemedialQuizId = quizId,
                        Score = isCorrect ? 100 : 0,
                        Passed = isCorrect,
                        TakenAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            // 🧹 تنظيف الجلسة المؤقتة
            foreach (var key in sessionKeys.Concat(quizKeys))
                http.Session.Remove(key);
        }





public async Task<RemedialSessionTrackingSummaryVm> GetSessionTrackingSummaryAsync(int sessionId)
    {
        using var _context = _contextFactory.CreateDbContext();

        // 🧩 جلب كل سجلات المشاهدة للجلسة
        var videoLogs = await _context.RemedialSessionLogs
            .Where(v => v.SessionId == sessionId)
            .ToListAsync();

        // 🧩 جلب كل نتائج الاختبارات التطبيقية للجلسة
        var quizResults = await (
            from q in _context.StudentRemedialQuizResults
            join s in _context.RemedialSessions on q.StudentId equals s.StudentID
            where s.Id == sessionId
            select q
        ).ToListAsync();

        // 🧮 تحليل البيانات
        int totalVideos = videoLogs.Count;
        int completedVideos = videoLogs.Count(v => v.IsCompleted);
        double totalWatchSeconds = videoLogs.Sum(v => v.DurationWatched);
        double avgQuizScore = quizResults.Any() ? quizResults.Average(q => q.Score) : 0;

        double progressPercent = totalVideos == 0
            ? 0
            : Math.Round(((double)completedVideos / totalVideos) * 100, 1);

        // ✅ بناء النموذج التحليلي
        return new RemedialSessionTrackingSummaryVm
        {
            SessionId = sessionId,
            TotalVideos = totalVideos,
            CompletedVideos = completedVideos,
            TotalWatchMinutes = Math.Round(totalWatchSeconds / 60, 1),
            AverageQuizScore = Math.Round(avgQuizScore, 1),
            ProgressPercent = progressPercent
        };
    }







    // ================================================================
    // 🔵 المرحلة الثالثة: بناء تقرير الجلسة العلاجية
    // ================================================================
    public async Task<RemedialSessionReportVm> BuildRemedialSessionReportAsync(int planId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var plan = await _context.RemedialPlans
                .Include(p => p.Student)
                .Include(p => p.Lessons)
                    .ThenInclude(l => l.Section)
                .FirstOrDefaultAsync(p => p.Id == planId);

            if (plan == null)
                throw new Exception("❌ لم يتم العثور على الخطة العلاجية.");

            // 🧩 حساب البيانات الأساسية من سجل المشاهدة
            var logs = await _context.RemedialSessionLogs
                .Where(l => l.SessionId == planId)
                .ToListAsync();

            int totalVideos = logs.Count;
            int completedVideos = logs.Count(l => l.IsCompleted);
            double totalWatchTime = logs.Sum(l => l.DurationWatched);

            // 🧩 حساب نتائج الاختبارات التطبيقية
            var quizResults = await _context.StudentRemedialQuizResults
                .Where(q => q.StudentId == plan.StudentID)
                .ToListAsync();

            double avgQuizScore = quizResults.Any() ? quizResults.Average(q => q.Score) : 0;

            // 🔹 حساب مستوى التحسن (الذكاء التحليلي)
            string level = CalculateAIAssessmentLevel(avgQuizScore);
            string recommendation = GenerateAIRecommendation(level, avgQuizScore, completedVideos, totalVideos);

            // ✅ تحديث الخطة العلاجية بالنتائج الجديدة
            plan.AIAssessedRiskScore = avgQuizScore;
            plan.AIAssessedLevel = level;
            plan.AIRecommendations = recommendation;
            plan.CompletedLessons = completedVideos;
            plan.TotalLessons = totalVideos;
            plan.IsCompleted = completedVideos == totalVideos;

            await _context.SaveChangesAsync();

            // 🔹 بناء التقرير النهائي للعرض
            return new RemedialSessionReportVm
            {
                StudentName = plan.Student.FullName,
                PlanTitle = plan.Title,
                TotalVideos = totalVideos,
                CompletedVideos = completedVideos,
                TotalWatchSeconds = (int)totalWatchTime,
                AverageQuizScore = Math.Round(avgQuizScore, 1),
                AIAssessedLevel = level,
                AIRecommendations = recommendation
            };
        }

        // ================================================================
        // 🧠 المرحلة الرابعة: التحليل الذكي (ذكاء اصطناعي مبسط)
        // ================================================================
        private string CalculateAIAssessmentLevel(double avgScore)
        {
            if (avgScore >= 90) return "تحسّن ممتاز";
            if (avgScore >= 70) return "تحسّن جيد";
            if (avgScore >= 50) return "تحسّن جزئي";
            return "يحتاج إعادة جلسة علاجية";
        }

        private string GenerateAIRecommendation(string level, double avgScore, int completedVideos, int totalVideos)
        {
            if (level == "تحسّن ممتاز")
                return "👏 أداء رائع! استمر بنفس الطريقة.";
            if (level == "تحسّن جيد")
                return "👍 تحسّن واضح، يُنصح بإعادة مراجعة المحور الذي واجهت صعوبة فيه.";
            if (level == "تحسّن جزئي")
                return "⚠️ تحسّن بسيط، يُوصى بإعادة مشاهدة بعض الفيديوهات وتكرار الاختبار.";
            return "❌ لم يتحقق التحسّن المطلوب، يُنصح بجدولة جلسة علاجية جديدة مع المدرب.";
        }

        // ================================================================
        // 🟣 المرحلة الخامسة: ملخص الأداء العام للجلسة
        // ================================================================

        // ================================================================
        // 🟢 دعم قديم (Log مباشر للمشاهدة أو التدريب)
        // ================================================================
        public async Task LogVideoWatchAsync(int sessionId, int videoId, int secondsWatched)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🧩 جلب بيانات الجلسة لتحديد الطالب
            var session = await _context.RemedialSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
                return; // ⚠️ الجلسة غير موجودة

            int studentId = session.StudentID;

            // 🧠 تحقق مما إذا كان السجل موجود مسبقًا
            var existing = await _context.RemedialSessionLogs
                .FirstOrDefaultAsync(r =>
                    r.StudentId == studentId &&
                    r.SessionId == sessionId &&
                    r.VideoId == videoId);

            if (existing != null)
            {
                // 🔄 تحديث السجل الحالي
                existing.DurationWatched = secondsWatched;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.WatchEnd = DateTime.UtcNow;
                existing.IsCompleted = secondsWatched > 60; // يمكنك تعديل العتبة
                _context.RemedialSessionLogs.Update(existing);
            }
            else
            {
                // ➕ إنشاء سجل جديد
                var log = new RemedialSessionLog
                {
                    StudentId = studentId,
                    SessionId = sessionId,
                    VideoId = videoId,
                    DurationWatched = secondsWatched,
                    WatchStart = DateTime.UtcNow,
                    WatchEnd = DateTime.UtcNow,
                    IsCompleted = secondsWatched > 60,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.RemedialSessionLogs.AddAsync(log);
            }

            await _context.SaveChangesAsync();
        }

        public async Task SaveQuizAttemptAsync(int studentId, int videoId, int quizId, bool isCorrect)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🧩 جلب بيانات الجلسة المرتبطة بالفيديو لتوثيقها
            var session = await _context.RemedialSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentID == studentId &&
                                          _context.RemedialSessionLogs.Any(l => l.VideoId == videoId && l.SessionId == s.Id));

            // 🔍 البحث عن محاولة سابقة
            var existing = await _context.StudentRemedialQuizResults
                .FirstOrDefaultAsync(q => q.StudentId == studentId && q.RemedialQuizId == quizId);

            if (existing != null)
            {
                // 🔄 تحديث المحاولة السابقة
                existing.Score = isCorrect ? 100 : 0;
                existing.Passed = isCorrect;
                existing.TakenAt = DateTime.UtcNow;
                _context.StudentRemedialQuizResults.Update(existing);
            }
            else
            {
                // ➕ إنشاء محاولة جديدة
                var attempt = new StudentRemedialQuizResult
                {
                    StudentId = studentId,
                    RemedialQuizId = quizId,
                    Score = isCorrect ? 100 : 0,
                    Passed = isCorrect,
                    TakenAt = DateTime.UtcNow
                };

                await _context.StudentRemedialQuizResults.AddAsync(attempt);
            }

            await _context.SaveChangesAsync();
        }







    }
}
