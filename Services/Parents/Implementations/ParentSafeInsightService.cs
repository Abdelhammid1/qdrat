using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Parents.Interfaces;
using QdratNew.ViewModels.Parents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Parents.Implementations
{
    public class ParentSafeInsightService : IParentSafeInsightService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ParentSafeInsightService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<ParentStudentInsightViewModel> GetSafeInsightAsync(int parentId, int studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var studentName = await db.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync() ?? "الطالب";

            // جلب الواجبات من دفعات الطالب (بدون IN على قوائم)
            var batchIds = await db.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.StudentID == studentId)
                .Select(e => e.BatchId)
                .ToListAsync();

            int totalHw = 0, completedHw = 0, lateHw = 0;

            if (batchIds.Count > 0)
            {
                // جلب الواجبات أولاً ثم التصفية في الذاكرة (SQL Server 2014 safe)
                var allSets = await db.HomeworkSets
                    .AsNoTracking()
                    .ToListAsync();

                var relevantSets = allSets
                    .Where(hs => batchIds.Contains(hs.BatchId))
                    .ToList();

                var setIds = relevantSets.Select(s => s.Id).ToList();

                var submittedIds = new HashSet<int>();
                if (setIds.Count > 0)
                {
                    var submitted = await db.HomeworkSetStudents
                        .AsNoTracking()
                        .Where(x => x.StudentId == studentId && x.IsSubmitted)
                        .Select(x => x.HomeworkSetId)
                        .ToListAsync();
                    submittedIds = new HashSet<int>(submitted);
                }

                totalHw = relevantSets.Count;
                completedHw = relevantSets.Count(s => submittedIds.Contains(s.Id));
                var now = DateTime.Now;
                lateHw = relevantSets.Count(s =>
                    !submittedIds.Contains(s.Id) &&
                    s.EndAt.HasValue && s.EndAt.Value < now);
            }

            int commitmentPct = totalHw > 0
                ? (int)Math.Round(completedHw * 100.0 / totalHw)
                : 100;

            // أداء الطالب من StudentPerformance
            var performances = await db.StudentPerformances
                .AsNoTracking()
                .Where(sp => sp.StudentID == studentId)
                .OrderByDescending(sp => sp.ExamDate)
                .Take(10)
                .Select(sp => sp.Score)
                .ToListAsync();

            double avgScore = performances.Count > 0 ? performances.Average() : 0;

            // تحديد المستوى العام
            string overallStatus, statusColor;
            if (avgScore >= 75 && commitmentPct >= 70)
            {
                overallStatus = "مستقر";
                statusColor = "success";
            }
            else if (avgScore >= 55 || commitmentPct >= 50)
            {
                overallStatus = "يحتاج متابعة";
                statusColor = "warning";
            }
            else
            {
                overallStatus = "يحتاج انتباه";
                statusColor = "danger";
            }

            // اتجاه التعلم
            string trend = "ثابت";
            string trendIcon = "fa-minus";
            string trendColor = "text-secondary";
            if (performances.Count >= 3)
            {
                var recent = performances.Take(3).Average();
                var older = performances.Skip(3).Any()
                    ? performances.Skip(3).Average()
                    : recent;
                if (recent > older + 5)
                {
                    trend = "يتحسن";
                    trendIcon = "fa-arrow-trend-up";
                    trendColor = "text-success";
                }
                else if (recent < older - 5)
                {
                    trend = "متراجع";
                    trendIcon = "fa-arrow-trend-down";
                    trendColor = "text-danger";
                }
            }

            bool canCreate = lateHw == 0 && commitmentPct >= 40;
            string? blockingReason = null;
            if (lateHw > 0)
                blockingReason = "الأفضل اليوم متابعة الواجبات المتأخرة قبل إنشاء تدريب جديد.";
            else if (commitmentPct < 40)
                blockingReason = "لا ننصح بإضافة اختبار جديد اليوم. الطالب لديه مهام تعليمية كافية.";

            return new ParentStudentInsightViewModel
            {
                StudentId = studentId,
                StudentName = studentName,
                OverallStatus = overallStatus,
                StatusColor = statusColor,
                CommitmentStatus = commitmentPct >= 75 ? "ممتاز" : commitmentPct >= 50 ? "جيد" : "يحتاج متابعة",
                LearningTrend = trend,
                TrendIcon = trendIcon,
                TrendColor = trendColor,
                SafeWeaknessSummary = avgScore < 60
                    ? "يحتاج الطالب إلى دعم إضافي في بعض جوانب المنهج."
                    : null,
                RecommendedAction = lateHw > 0
                    ? "تابع واجبًا متأخرًا"
                    : avgScore < 60 ? "ابدأ اختبار تعزيز قصير" : "اقرأ التقرير الأسبوعي",
                CanRequestSmartPractice = canCreate,
                BlockingReason = blockingReason,
                CommitmentPercent = commitmentPct
            };
        }

        public async Task<ParentLearningStatusViewModel> GetLearningStatusAsync(int parentId, int studentId)
        {
            var insight = await GetSafeInsightAsync(parentId, studentId);

            using var db = _contextFactory.CreateDbContext();

            var batchIds = await db.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.StudentID == studentId)
                .Select(e => e.BatchId)
                .ToListAsync();

            int totalHw = 0, completedHw = 0, lateHw = 0;
            var now = DateTime.Now;

            if (batchIds.Count > 0)
            {
                var allSets = await db.HomeworkSets.AsNoTracking().ToListAsync();
                var relevantSets = allSets.Where(hs => batchIds.Contains(hs.BatchId)).ToList();
                var setIds = relevantSets.Select(s => s.Id).ToList();

                var submitted = new HashSet<int>();
                if (setIds.Count > 0)
                {
                    var subs = await db.HomeworkSetStudents
                        .AsNoTracking()
                        .Where(x => x.StudentId == studentId && x.IsSubmitted)
                        .Select(x => x.HomeworkSetId)
                        .ToListAsync();
                    submitted = new HashSet<int>(subs);
                }

                totalHw = relevantSets.Count;
                completedHw = relevantSets.Count(s => submitted.Contains(s.Id));
                lateHw = relevantSets.Count(s =>
                    !submitted.Contains(s.Id) &&
                    s.EndAt.HasValue && s.EndAt.Value < now);
            }

            var upcomingExam = await db.ExamAssignmentsToStudents
                .AsNoTracking()
                .Where(e => e.StudentId == studentId && e.ScheduledDate > now)
                .OrderBy(e => e.ScheduledDate)
                .Select(e => e.ScheduledDate)
                .FirstOrDefaultAsync();

            var lastScore = await db.StudentPerformances
                .AsNoTracking()
                .Where(r => r.StudentID == studentId)
                .OrderByDescending(r => r.ExamDate)
                .Select(r => (double?)r.Score)
                .FirstOrDefaultAsync();

            return new ParentLearningStatusViewModel
            {
                StudentId = studentId,
                StudentName = insight.StudentName,
                CommitmentLevel = insight.CommitmentStatus,
                CommitmentPercent = insight.CommitmentPercent,
                PerformanceTrend = insight.LearningTrend,
                TrendIcon = insight.TrendIcon,
                TrendColor = insight.TrendColor,
                WeaknessSummary = insight.SafeWeaknessSummary,
                OverallStatus = insight.OverallStatus,
                StatusColor = insight.StatusColor,
                TotalHomeworks = totalHw,
                CompletedHomeworks = completedHw,
                LateHomeworks = lateHw,
                LastExamScore = lastScore ?? 0,
                HasUpcomingExam = upcomingExam.HasValue,
                UpcomingExamDate = upcomingExam.HasValue
                    ? upcomingExam.Value.ToString("yyyy/MM/dd")
                    : null
            };
        }

        public async Task<ParentHomeworkFollowUpViewModel> GetHomeworkFollowUpAsync(int studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var studentName = await db.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync() ?? "الطالب";

            var batchIds = await db.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.StudentID == studentId)
                .Select(e => e.BatchId)
                .ToListAsync();

            var allSets = await db.HomeworkSets.AsNoTracking().ToListAsync();
            var relevantSets = allSets.Where(hs => batchIds.Contains(hs.BatchId)).ToList();
            var setIds = relevantSets.Select(s => s.Id).ToList();

            var now = DateTime.Now;
            var submitted = new HashSet<int>();

            if (setIds.Count > 0)
            {
                var subs = await db.HomeworkSetStudents
                    .AsNoTracking()
                    .Where(x => x.StudentId == studentId && x.IsSubmitted)
                    .Select(x => x.HomeworkSetId)
                    .ToListAsync();
                submitted = new HashSet<int>(subs);
            }

            int total = relevantSets.Count;
            int completed = relevantSets.Count(s => submitted.Contains(s.Id));
            int late = relevantSets.Count(s =>
                !submitted.Contains(s.Id) && s.EndAt.HasValue && s.EndAt.Value < now);
            int pending = total - completed - late;

            int pct = total > 0 ? (int)Math.Round(completed * 100.0 / total) : 100;

            var recentSummary = relevantSets
                .OrderByDescending(s => s.StartAt)
                .Take(5)
                .Select(s => new HomeworkItemSummary
                {
                    Title = s.CompletionTitle ?? "واجب",
                    Status = submitted.Contains(s.Id)
                        ? "مكتمل"
                        : (s.EndAt.HasValue && s.EndAt.Value < now ? "متأخر" : "مطلوب"),
                    StatusColor = submitted.Contains(s.Id)
                        ? "success"
                        : (s.EndAt.HasValue && s.EndAt.Value < now ? "danger" : "warning"),
                    DueDate = s.EndAt.HasValue
                        ? s.EndAt.Value.ToString("yyyy/MM/dd")
                        : null
                })
                .ToList();

            return new ParentHomeworkFollowUpViewModel
            {
                StudentId = studentId,
                StudentName = studentName,
                TotalAssigned = total,
                Completed = completed,
                Late = late,
                Pending = pending < 0 ? 0 : pending,
                CommitmentLabel = pct >= 75 ? "ممتاز" : pct >= 50 ? "جيد" : "يحتاج متابعة",
                CommitmentColor = pct >= 75 ? "success" : pct >= 50 ? "warning" : "danger",
                SafeRecommendation = late > 0
                    ? "يُنصح بمتابعة الطالب في إكمال الواجبات المتأخرة في أقرب وقت."
                    : pct >= 80 ? "الطالب ملتزم بشكل ممتاز بالواجبات." : "الطالب بحاجة إلى تشجيع للالتزام بالواجبات.",
                RecentHomeworks = recentSummary
            };
        }

        public async Task<ParentExamFollowUpViewModel> GetExamFollowUpAsync(int studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var studentName = await db.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync() ?? "الطالب";

            var now = DateTime.Now;

            var results = await db.StudentPerformances
                .AsNoTracking()
                .Where(r => r.StudentID == studentId)
                .OrderByDescending(r => r.ExamDate)
                .Take(5)
                .Select(r => new { r.Score, r.ExamDate, r.ExamId })
                .ToListAsync();

            string trend = "ثابت";
            string trendIcon = "fa-minus";
            string trendColor = "text-secondary";

            if (results.Count >= 2)
            {
                double latest = results[0].Score;
                double prev = results[1].Score;
                if (latest > prev + 5)
                {
                    trend = "يتحسن";
                    trendIcon = "fa-arrow-trend-up";
                    trendColor = "text-success";
                }
                else if (latest < prev - 5)
                {
                    trend = "متراجع";
                    trendIcon = "fa-arrow-trend-down";
                    trendColor = "text-danger";
                }
            }

            var upcomingExam = await db.ExamAssignmentsToStudents
                .AsNoTracking()
                .Where(e => e.StudentId == studentId && e.ScheduledDate > now)
                .OrderBy(e => e.ScheduledDate)
                .Select(e => e.ScheduledDate)
                .FirstOrDefaultAsync();

            double? lastScore = results.Count > 0 ? results[0].Score : null;
            bool needsBoost = lastScore.HasValue && lastScore.Value < 65;

            var resultSummaries = results.Select((r, i) => new ExamResultSummary
            {
                ExamTitle = $"اختبار {i + 1}",
                Score = r.Score,
                ScoreColor = r.Score >= 75 ? "success" : r.Score >= 55 ? "warning" : "danger",
                ExamDate = r.ExamDate.ToString("yyyy/MM/dd"),
                Trend = "—"
            }).ToList();

            return new ParentExamFollowUpViewModel
            {
                StudentId = studentId,
                StudentName = studentName,
                LastExamScore = lastScore,
                PerformanceTrend = trend,
                TrendIcon = trendIcon,
                TrendColor = trendColor,
                HasUpcomingExam = upcomingExam.HasValue,
                UpcomingExamDate = upcomingExam.HasValue
                    ? upcomingExam.Value.ToString("yyyy/MM/dd")
                    : null,
                NeedsBoostPractice = needsBoost,
                SafeRecommendation = needsBoost
                    ? "يُنصح بتنفيذ تدريب تعزيزي قصير قبل الاختبار القادم."
                    : "استمر في التشجيع والمتابعة.",
                RecentResults = resultSummaries
            };
        }

        public async Task<ParentWeeklyReportViewModel> GetWeeklyReportAsync(int studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var studentName = await db.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync() ?? "الطالب";

            var weekStart = DateTime.Now.AddDays(-7);
            var weekEnd = DateTime.Now;

            var batchIds = await db.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.StudentID == studentId)
                .Select(e => e.BatchId)
                .ToListAsync();

            var allSets = await db.HomeworkSets.AsNoTracking().ToListAsync();
            var weekSets = allSets
                .Where(hs => batchIds.Contains(hs.BatchId) &&
                             hs.StartAt >= weekStart && hs.StartAt <= weekEnd)
                .ToList();

            var submitted = new HashSet<int>();
            if (weekSets.Count > 0)
            {
                var subs = await db.HomeworkSetStudents
                    .AsNoTracking()
                    .Where(x => x.StudentId == studentId && x.IsSubmitted)
                    .Select(x => x.HomeworkSetId)
                    .ToListAsync();
                submitted = new HashSet<int>(subs);
            }

            int completedHw = weekSets.Count(s => submitted.Contains(s.Id));
            int lateHw = weekSets.Count(s =>
                !submitted.Contains(s.Id) && s.EndAt.HasValue && s.EndAt.Value < DateTime.Now);

            int pct = weekSets.Count > 0
                ? (int)Math.Round(completedHw * 100.0 / weekSets.Count)
                : 100;

            var lastResult = await db.StudentPerformances
                .AsNoTracking()
                .Where(r => r.StudentID == studentId && r.ExamDate >= weekStart)
                .OrderByDescending(r => r.ExamDate)
                .Select(r => (double?)r.Score)
                .FirstOrDefaultAsync();

            return new ParentWeeklyReportViewModel
            {
                StudentId = studentId,
                StudentName = studentName,
                WeekRange = $"{weekStart:yyyy/MM/dd} — {weekEnd:yyyy/MM/dd}",
                CommitmentSummary = pct >= 75
                    ? "التزام ممتاز هذا الأسبوع"
                    : pct >= 50
                        ? "التزام جيد هذا الأسبوع"
                        : "يحتاج الطالب إلى تحسين الالتزام",
                CompletedHomeworks = completedHw,
                LateHomeworks = lateHw,
                ExamSummary = lastResult.HasValue
                    ? $"آخر نتيجة: {lastResult.Value:F0}%"
                    : "لا توجد اختبارات هذا الأسبوع",
                BestScore = lastResult,
                ImprovementNote = pct >= 80
                    ? "أداء الطالب هذا الأسبوع مميز."
                    : "يمكن تحسين الأداء بمزيد من التدريب.",
                WeekRecommendation = lateHw > 0
                    ? $"ننصح بإكمال {lateHw} واجب متأخر قبل نهاية الأسبوع."
                    : "أداء الطالب هذا الأسبوع جيد، استمر في التشجيع.",
                PlatformActionSummary = "تم تحليل أداء الطالب وتقديم توصيات مناسبة.",
                CommitmentColor = pct >= 75 ? "success" : pct >= 50 ? "warning" : "danger",
                CommitmentIcon = pct >= 75 ? "fa-check-circle" : "fa-exclamation-circle"
            };
        }

        public async Task<ParentMonthlyReportViewModel> GetMonthlyReportAsync(int studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var studentName = await db.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync() ?? "الطالب";

            var monthStart = DateTime.Now.AddDays(-30);

            var batchIds = await db.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.StudentID == studentId)
                .Select(e => e.BatchId)
                .ToListAsync();

            var allSets = await db.HomeworkSets.AsNoTracking().ToListAsync();
            var monthSets = allSets
                .Where(hs => batchIds.Contains(hs.BatchId) && hs.StartAt >= monthStart)
                .ToList();

            var submitted = new HashSet<int>();
            if (monthSets.Count > 0)
            {
                var subs = await db.HomeworkSetStudents
                    .AsNoTracking()
                    .Where(x => x.StudentId == studentId && x.IsSubmitted)
                    .Select(x => x.HomeworkSetId)
                    .ToListAsync();
                submitted = new HashSet<int>(subs);
            }

            int completedHw = monthSets.Count(s => submitted.Contains(s.Id));

            var practiceCount = await db.Set<QdratNew.Entities.ParentSmartPracticeRequest>()
                .AsNoTracking()
                .Where(r => r.StudentId == studentId && r.CreatedAt >= monthStart)
                .CountAsync();

            var results = await db.StudentPerformances
                .AsNoTracking()
                .Where(r => r.StudentID == studentId && r.ExamDate >= monthStart)
                .OrderByDescending(r => r.ExamDate)
                .Select(r => r.Score)
                .ToListAsync();

            string trend = "ثابت";
            string trendColor = "secondary";
            string trendIcon = "fa-minus";

            if (results.Count >= 2)
            {
                double firstHalf = results.Skip(results.Count / 2).Average();
                double secondHalf = results.Take(results.Count / 2).Average();
                if (secondHalf > firstHalf + 5)
                {
                    trend = "تحسن";
                    trendColor = "success";
                    trendIcon = "fa-arrow-trend-up";
                }
                else if (secondHalf < firstHalf - 5)
                {
                    trend = "تراجع";
                    trendColor = "danger";
                    trendIcon = "fa-arrow-trend-down";
                }
            }

            return new ParentMonthlyReportViewModel
            {
                StudentId = studentId,
                StudentName = studentName,
                MonthLabel = $"{monthStart:yyyy/MM/dd} — {DateTime.Now:yyyy/MM/dd}",
                OverallTrend = trend,
                TrendColor = trendColor,
                TrendIcon = trendIcon,
                ImprovementPoints = results.Count > 0 && results.Average() >= 65
                    ? new List<string> { "أداء عام فوق المتوسط." }
                    : new List<string>(),
                FollowUpPoints = completedHw < monthSets.Count
                    ? new List<string> { "يوجد واجبات لم تكتمل." }
                    : new List<string>(),
                PlatformImpactSummary = "تم رصد الأداء وتقديم توصيات مناسبة طوال الشهر.",
                TotalHomeworks = monthSets.Count,
                CompletedHomeworks = completedHw,
                TotalPracticeRequests = practiceCount,
                MonthRecommendation = trend == "تراجع"
                    ? "ننصح بمتابعة أوثق مع الطالب في الفترة القادمة."
                    : "استمر في تشجيع الطالب ومتابعته."
            };
        }
    }
}
