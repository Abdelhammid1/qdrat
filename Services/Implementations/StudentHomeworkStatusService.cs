using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Homework;

namespace QdratNew.Services.Implementations
{
    public class StudentHomeworkStatusService : IStudentHomeworkStatusService
    {
        private const string StatusSolved = "تم الحل";
        private const string StatusLate = "متأخر";
        private const string StatusRequired = "مطلوب";

        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ITimeZoneService _timeZoneService;

        public StudentHomeworkStatusService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ITimeZoneService timeZoneService)
        {
            _contextFactory = contextFactory;
            _timeZoneService = timeZoneService;
        }

        public async Task<StudentHomeworkSummaryVm> GetHomeworkSummaryAsync(
            int studentId,
            int courseId,
            int batchId)
        {
            var all = await LoadHomeworkListAsync(studentId, courseId, batchId);

            return BuildSummary(all);
        }

        public async Task<List<HomeworkListVm>> GetAllAsync(
            int studentId,
            int courseId,
            int batchId)
        {
            return await LoadHomeworkListAsync(studentId, courseId, batchId);
        }

        public async Task<List<HomeworkListVm>> GetRequiredAsync(
            int studentId,
            int courseId,
            int batchId)
        {
            var all = await LoadHomeworkListAsync(studentId, courseId, batchId);

            return FilterRequired(all);
        }

        public async Task<List<HomeworkListVm>> GetSolvedAsync(
            int studentId,
            int courseId,
            int batchId)
        {
            var all = await LoadHomeworkListAsync(studentId, courseId, batchId);

            return FilterSolved(all);
        }

        public async Task<List<HomeworkListVm>> GetLateAsync(
            int studentId,
            int courseId,
            int batchId)
        {
            var all = await LoadHomeworkListAsync(studentId, courseId, batchId);

            return FilterLate(all);
        }

        public async Task<(List<HomeworkListVm> Items, StudentHomeworkSummaryVm Summary)> GetRequiredWithSummaryAsync(
            int studentId,
            int courseId,
            int batchId)
        {
            var all = await LoadHomeworkListAsync(studentId, courseId, batchId);

            return (
                FilterRequired(all),
                BuildSummary(all)
            );
        }

        public async Task<(List<HomeworkListVm> Items, StudentHomeworkSummaryVm Summary)> GetSolvedWithSummaryAsync(
            int studentId,
            int courseId,
            int batchId)
        {
            var all = await LoadHomeworkListAsync(studentId, courseId, batchId);

            return (
                FilterSolved(all),
                BuildSummary(all)
            );
        }

        public async Task<(List<HomeworkListVm> Items, StudentHomeworkSummaryVm Summary)> GetLateWithSummaryAsync(
            int studentId,
            int courseId,
            int batchId)
        {
            var all = await LoadHomeworkListAsync(studentId, courseId, batchId);

            return (
                FilterLate(all),
                BuildSummary(all)
            );
        }

        private async Task<List<HomeworkListVm>> LoadHomeworkListAsync(
            int studentId,
            int courseId,
            int batchId)
        {
            using var db = _contextFactory.CreateDbContext();

            var now = _timeZoneService.GetNowSaudi();

            var stateRows = await (
                from hss in db.HomeworkSetStudents.AsNoTracking()
                join hs in db.HomeworkSets.AsNoTracking()
                    on hss.HomeworkSetId equals hs.Id
                join b in db.Batches.AsNoTracking()
                    on hs.BatchId equals b.Id
                where hss.StudentId == studentId
                      && hs.BatchId == batchId
                      && b.CourseId == courseId
                      && (!hs.StartAt.HasValue || hs.StartAt.Value <= now)
                select new HomeworkSetStudentListProjection
                {
                    Id = hs.Id,
                    Title = hs.Title,
                    CompletionTitle = hs.CompletionTitle,
                    StartAt = hs.StartAt,
                    EndAt = hs.EndAt,
                    BatchName = b.Name,
                    IsSubmitted = hss.IsSubmitted,
                    Score = hss.Score
                }
            ).ToListAsync();

            var homeworkRows = await (
                from h in db.Homeworks.AsNoTracking()
                join hs in db.HomeworkSets.AsNoTracking()
                    on h.HomeworkSetId equals hs.Id
                join b in db.Batches.AsNoTracking()
                    on hs.BatchId equals b.Id
                where h.StudentId == studentId
                      && hs.BatchId == batchId
                      && b.CourseId == courseId
                      && (!hs.StartAt.HasValue || hs.StartAt.Value <= now)
                select new HomeworkSetListProjection
                {
                    Id = hs.Id,
                    Title = hs.Title,
                    CompletionTitle = hs.CompletionTitle,
                    StartAt = hs.StartAt,
                    EndAt = hs.EndAt,
                    BatchName = b.Name
                }
            ).ToListAsync();

            var visibleHomeworkSets = BuildVisibleHomeworkSets(stateRows, homeworkRows);

            var result = new List<HomeworkListVm>(visibleHomeworkSets.Count);

            foreach (var homeworkSet in visibleHomeworkSets.OrderByDescending(x => x.StartAt ?? x.EndAt ?? now))
            {
                var delayLevel = ResolveDelayLevel(
                    homeworkSet.IsSubmitted,
                    homeworkSet.EndAt,
                    now
                );

                result.Add(new HomeworkListVm
                {
                    HomeworkSetId = homeworkSet.Id,
                    Title = string.IsNullOrWhiteSpace(homeworkSet.Title)
                        ? "واجب"
                        : homeworkSet.Title,
                    CreatedAt = homeworkSet.StartAt ?? now,
                    SectionName = homeworkSet.BatchName,
                    IsSubmitted = homeworkSet.IsSubmitted,
                    DelayLevel = delayLevel,
                    Score = homeworkSet.IsSubmitted
                        ? Math.Round(homeworkSet.Score ?? 0, 1)
                        : 0,
                    BatchAverageScore = 0
                });
            }

            return result;
        }

        private static List<HomeworkSetStudentListProjection> BuildVisibleHomeworkSets(
            List<HomeworkSetStudentListProjection> stateRows,
            List<HomeworkSetListProjection> homeworkRows)
        {
            var dictionary = new Dictionary<int, HomeworkSetStudentListProjection>();

            foreach (var state in stateRows)
            {
                if (!dictionary.ContainsKey(state.Id))
                {
                    dictionary.Add(state.Id, state);
                }
            }

            foreach (var homework in homeworkRows)
            {
                if (!dictionary.ContainsKey(homework.Id))
                {
                    dictionary.Add(homework.Id, new HomeworkSetStudentListProjection
                    {
                        Id = homework.Id,
                        Title = homework.Title,
                        CompletionTitle = homework.CompletionTitle,
                        StartAt = homework.StartAt,
                        EndAt = homework.EndAt,
                        BatchName = homework.BatchName,
                        IsSubmitted = false,
                        Score = null
                    });
                }
            }

            return dictionary.Values.ToList();
        }

        private static string ResolveDelayLevel(
            bool isSubmitted,
            DateTime? endAt,
            DateTime now)
        {
            if (isSubmitted)
            {
                return StatusSolved;
            }

            if (endAt.HasValue && endAt.Value < now)
            {
                return StatusLate;
            }

            return StatusRequired;
        }

        private static List<HomeworkListVm> FilterRequired(
            List<HomeworkListVm> all)
        {
            return all
                .Where(x => !x.IsSubmitted && x.DelayLevel == StatusRequired)
                .ToList();
        }

        private static List<HomeworkListVm> FilterSolved(
            List<HomeworkListVm> all)
        {
            return all
                .Where(x => x.IsSubmitted)
                .ToList();
        }

        private static List<HomeworkListVm> FilterLate(
            List<HomeworkListVm> all)
        {
            return all
                .Where(x => x.DelayLevel == StatusLate)
                .ToList();
        }

        private static StudentHomeworkSummaryVm BuildSummary(
            List<HomeworkListVm> all)
        {
            var completedCount = 0;
            var requiredCount = 0;
            var lateCount = 0;

            double completedScoreTotal = 0;

            foreach (var item in all)
            {
                if (item.IsSubmitted)
                {
                    completedCount++;
                    completedScoreTotal += item.Score;
                    continue;
                }

                if (item.DelayLevel == StatusLate)
                {
                    lateCount++;
                    continue;
                }

                if (item.DelayLevel == StatusRequired)
                {
                    requiredCount++;
                }
            }

            return new StudentHomeworkSummaryVm
            {
                Total = all.Count,
                Completed = completedCount,
                Required = requiredCount,
                Late = lateCount,
                AverageScore = completedCount > 0
                    ? Math.Round(completedScoreTotal / completedCount, 1)
                    : 0
            };
        }

        private class HomeworkSetListProjection
        {
            public int Id { get; set; }
            public string? Title { get; set; }
            public string? CompletionTitle { get; set; }
            public DateTime? StartAt { get; set; }
            public DateTime? EndAt { get; set; }
            public string BatchName { get; set; } = string.Empty;
        }

        private sealed class HomeworkSetStudentListProjection : HomeworkSetListProjection
        {
            public bool IsSubmitted { get; set; }
            public double? Score { get; set; }
        }
    }
}
