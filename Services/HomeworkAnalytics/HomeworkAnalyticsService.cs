using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Instructor.Homework;

namespace QdratNew.Services.HomeworkAnalytics
{
    public class HomeworkAnalyticsService : IHomeworkAnalyticsService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public HomeworkAnalyticsService(
            IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<HomeworkGlobalReportVM> BuildGlobalReportAsync(int homeworkSetId)
        {
            using var db = _contextFactory.CreateDbContext();

            // الطلاب
            // ============================
            // الطلاب
            // ============================

            var students = await db.HomeworkSetStudents
                .AsNoTracking()
                .Where(x => x.HomeworkSetId == homeworkSetId)
                .Include(x => x.Student)
                .ToListAsync();

            int totalStudents = students.Count;

            int submittedStudents = students
                .Count(x => x.IsSubmitted);

            int notSubmittedStudents = totalStudents - submittedStudents;

            decimal averageScore = students.Any()
                ? Math.Round((decimal)(students.Average(x => x.Score) ?? 0), 2)
                : 0;

            double successRate = totalStudents > 0
                ? Math.Round(submittedStudents * 100.0 / totalStudents, 2)
                : 0;


            // أفضل الطلاب
            var topStudents = students
                .Where(x => x.IsSubmitted)
                .OrderByDescending(x => x.Score)
                .Take(5)
                .Select(x => new StudentRankingVM
                {
                    StudentId = x.StudentId,
                    StudentName = x.Student.FullName,
                    Score = x.Score.HasValue ? (decimal)x.Score.Value : 0m,
                    SubmittedAt = x.SubmittedAt
                })
                .ToList();


            // أضعف الطلاب
            var weakStudents = students
                .Where(x => x.IsSubmitted)
                .OrderBy(x => x.Score)
                .Take(5)
                .Select(x => new StudentRankingVM
                {
                    StudentId = x.StudentId,
                    StudentName = x.Student.FullName,
                    Score = x.Score.HasValue ? (decimal)x.Score.Value : 0m,
                    SubmittedAt = x.SubmittedAt
                })
                .ToList();

            // المحاولات
            var attempts = await db.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.HomeworkSetId == homeworkSetId)
                .ToListAsync();

            var questionIds = attempts
                .Select(x => x.QuestionId)
                .Distinct()
                .ToList();

            var questionIdSet = questionIds.ToHashSet();

            // الأسئلة
            var allQuestions = await db.Questions
                .AsNoTracking()
                .Select(q => new
                {
                    q.Id,
                    q.Title
                })
                .ToListAsync();

            var questions = allQuestions
                .Where(q => questionIdSet.Contains(q.Id))
                .ToDictionary(q => q.Id, q => q.Title);

            // تحليل الأسئلة
            var grouped = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g =>
                {
                    int correct = g.Count(x => x.IsCorrect);
                    int wrong = g.Count(x => !x.IsCorrect);
                    int answered = g.Count();

                    int skipped = totalStudents - answered;

                    double accuracy = answered > 0
                        ? Math.Round(correct * 100.0 / answered, 2)
                        : 0;

                    double avgTime = g.Any()
                        ? g.Average(x => x.TimeTakenSeconds)
                        : 0;

                    return new QuestionGlobalStatVM
                    {
                        QuestionId = g.Key,
                        QuestionTitle = questions.ContainsKey(g.Key)
                            ? questions[g.Key]
                            : "",

                        CorrectAnswers = correct,
                        WrongAnswers = wrong,
                        SkippedAnswers = skipped,
                        Accuracy = accuracy,
                        AverageTimeSeconds = Math.Round(avgTime, 1)
                    };
                })
                .OrderByDescending(x => x.Accuracy)
                .ToList();

            // أصعب الأسئلة
            var hardestQuestions = grouped
                .OrderBy(x => x.Accuracy)
                .Take(5)
                .ToList();

            // أسهل الأسئلة
            var easiestQuestions = grouped
                .OrderByDescending(x => x.Accuracy)
                .Take(5)
                .ToList();

            return new HomeworkGlobalReportVM
            {
                HomeworkSetId = homeworkSetId,
                TotalStudents = totalStudents,
                SubmittedStudents = submittedStudents,
                NotSubmittedStudents = notSubmittedStudents,
                AverageScore = averageScore,
                SuccessRate = successRate,
                Questions = grouped,
                HardestQuestions = hardestQuestions,
                EasiestQuestions = easiestQuestions,
                TopStudents = topStudents,
                WeakStudents = weakStudents
            };
        }
    }
}