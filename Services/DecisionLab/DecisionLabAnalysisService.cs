using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Admin.DecisionLab;

namespace QdratNew.Services.DecisionLab
{
    public class DecisionLabAnalysisService : IDecisionLabAnalysisService
    {
        private readonly ApplicationDbContext _context;

        public DecisionLabAnalysisService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DecisionLabIndexViewModel> BuildIndexAsync(
            int? batchId,
            int? curriculumId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            // ── Batches ───────────────────────────────────────────────────────
            var batchesQuery =
                from batch in _context.Batches.AsNoTracking()
                join course in _context.Courses.AsNoTracking()
                    on batch.CourseId equals course.Id
                where !batch.IsDeleted && !batch.IsArchived
                select new DecisionLabBatchAnalysisViewModel
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    CourseId = course.Id,
                    CourseName = course.Name,
                    CurriculumId = curriculumId,
                    ActiveStudentsCount = _context.StudentBatchEnrollments
                        .AsNoTracking()
                        .Count(enrollment =>
                            enrollment.BatchId == batch.Id
                            && (enrollment.Status == "Active" || enrollment.Status == "نشط"))
                };

            // دائماً نحمل كل الدفعات للـ sidebar بغض النظر عن الـ batchId المختار
            var batches = await batchesQuery.OrderBy(b => b.BatchName).ToListAsync();

            var now = DateTime.Now;
            var monthAgo = now.AddDays(-30);
            var sixWeeksAgo = now.AddDays(-42);

            // ── Homework data (single load for multiple KPIs + chart) ─────────
            var allSentHwData = await (
                from hss in _context.HomeworkSetStudents.AsNoTracking()
                join hs in _context.HomeworkSets.AsNoTracking() on hss.HomeworkSetId equals hs.Id
                where hs.IsSent
                select new
                {
                    hss.StudentId,
                    hss.IsSubmitted,
                    hss.Score,
                    hs.CreatedAt,
                    hs.BatchId
                }
            ).ToListAsync();

            // ── Overall avg score & homework completion ───────────────────────
            double overallAvgScore = allSentHwData.Any(x => x.IsSubmitted && x.Score.HasValue)
                ? Math.Round(allSentHwData.Where(x => x.IsSubmitted && x.Score.HasValue).Average(x => x.Score!.Value), 1)
                : 0;
            double hwCompletionPct = allSentHwData.Any()
                ? Math.Round(allSentHwData.Count(x => x.IsSubmitted) * 100.0 / allSentHwData.Count, 1)
                : 0;

            // ── Risk distribution (per-student homework submission rate) ──────
            var perStudentStats = allSentHwData
                .GroupBy(x => x.StudentId)
                .Select(g => new { Total = g.Count(), Submitted = g.Count(x => x.IsSubmitted) })
                .ToList();

            int criticalCount = 0, highRiskCount = 0, mediumCount = 0, lowCount = 0;
            foreach (var s in perStudentStats)
            {
                double rate = s.Total > 0 ? s.Submitted * 100.0 / s.Total : 100;
                if (rate < 30) criticalCount++;
                else if (rate < 50) highRiskCount++;
                else if (rate < 70) mediumCount++;
                else lowCount++;
            }

            int totalClassified = criticalCount + highRiskCount + mediumCount + lowCount;
            var riskDist = new List<RiskDistributionItem>
            {
                new() { Label = "منخفض", Count = lowCount,      Percentage = totalClassified > 0 ? Math.Round(lowCount      * 100.0 / totalClassified, 1) : 0, Color = "#027a50" },
                new() { Label = "متوسط", Count = mediumCount,   Percentage = totalClassified > 0 ? Math.Round(mediumCount   * 100.0 / totalClassified, 1) : 0, Color = "#c47c00" },
                new() { Label = "مرتفع", Count = highRiskCount, Percentage = totalClassified > 0 ? Math.Round(highRiskCount * 100.0 / totalClassified, 1) : 0, Color = "#e05a00" },
                new() { Label = "حرج",   Count = criticalCount, Percentage = totalClassified > 0 ? Math.Round(criticalCount * 100.0 / totalClassified, 1) : 0, Color = "#c8202a" },
            };

            // ── Critical issues (batches with very low homework completion) ───
            var criticalIssues = allSentHwData
                .GroupBy(x => x.BatchId)
                .Where(g => g.Count() > 0)
                .Select(g => new
                {
                    BatchId = g.Key,
                    TotalAssigned = g.Count(),
                    TotalSubmitted = g.Count(x => x.IsSubmitted),
                    Pct = g.Count(x => x.IsSubmitted) * 100.0 / g.Count()
                })
                .Where(x => x.Pct < 50)
                .OrderBy(x => x.Pct)
                .Take(10)
                .Select((x, idx) =>
                {
                    var bInfo = batches.FirstOrDefault(b => b.BatchId == x.BatchId);
                    double pct = Math.Round(x.Pct, 1);
                    return new DecisionLabCriticalIssueViewModel
                    {
                        Id = idx + 1,
                        BatchId = x.BatchId,
                        SeverityCss = pct < 30 ? "critical" : "high",
                        SeverityLabel = pct < 30 ? "حرج" : "مرتفع",
                        TypeLabel = "إكمال الواجبات",
                        Title = $"نسبة تسليم منخفضة في دفعة {bInfo?.BatchName ?? $"#{x.BatchId}"}",
                        AffectedStudentsCount = bInfo?.ActiveStudentsCount ?? 0,
                        MetricLabel = "نسبة التسليم",
                        MetricValue = $"{pct:0}%",
                        TrendDirection = "down",
                        TrendText = $"تراجع إلى {pct:0}% من المتوقع 100%"
                    };
                })
                .ToList();

            // ── Lesson completions ────────────────────────────────────────────
            var lessonCompletions = new List<LessonCompletionItem>();
            int totalActiveBatches = batches.Count;
            if (totalActiveBatches > 0)
            {
                var rawLessonData = await (
                    from blc in _context.BatchLessonCompletions.AsNoTracking()
                    join lesson in _context.Lessons.AsNoTracking() on blc.LessonId equals lesson.Id
                    where lesson.IsActive && (!batchId.HasValue || blc.BatchId == batchId.Value)
                    group blc by new { blc.LessonId, lesson.Title } into g
                    select new { g.Key.Title, BatchCount = g.Select(x => x.BatchId).Distinct().Count() }
                ).OrderByDescending(x => x.BatchCount).Take(10).ToListAsync();

                lessonCompletions = rawLessonData.Select(x => new LessonCompletionItem
                {
                    LessonTitle = x.Title,
                    CompletionPct = Math.Round(x.BatchCount * 100.0 / totalActiveBatches, 1)
                }).ToList();
            }

            // ── Recommendation KPIs ───────────────────────────────────────────
            var approvedCount = await _context.DecisionRecommendations
                .AsNoTracking()
                .CountAsync(r => r.Status == "Approved" && r.CreatedAt >= monthAgo);

            var pendingCount = await _context.DecisionRecommendations
                .AsNoTracking()
                .CountAsync(r => r.Status == "PendingReview");

            var activeTasksCount = await _context.InterventionTasks
                .AsNoTracking()
                .CountAsync(t => t.Status == "Assigned" || t.Status == "InProgress");

            // ── Recent recommendations table ──────────────────────────────────
            var recentRecRaw = await _context.DecisionRecommendations
                .AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .Take(20)
                .Select(r => new { r.Id, r.Summary, r.CreatedAt, r.Status, r.BatchId })
                .ToListAsync();

            var recBatchIds = recentRecRaw
                .Where(r => r.BatchId.HasValue)
                .Select(r => r.BatchId!.Value)
                .Distinct()
                .ToList();

            var recBatchNames = recBatchIds.Any()
                ? await _context.Batches.AsNoTracking()
                    .Where(b => recBatchIds.Contains(b.Id))
                    .ToDictionaryAsync(b => b.Id, b => b.Name)
                : new Dictionary<int, string>();

            var recIds = recentRecRaw.Select(r => r.Id).ToList();
            var taskCountMap = recIds.Any()
                ? await _context.InterventionTasks.AsNoTracking()
                    .Where(t => t.DecisionRecommendationId.HasValue && recIds.Contains(t.DecisionRecommendationId.Value))
                    .GroupBy(t => t.DecisionRecommendationId!.Value)
                    .Select(g => new { RecId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.RecId, x => x.Count)
                : new Dictionary<int, int>();

            var recentRecommendations = recentRecRaw.Select(r => new RecentRecommendationItem
            {
                Id = r.Id,
                Title = r.Summary,
                CreatedAt = r.CreatedAt,
                BatchName = r.BatchId.HasValue && recBatchNames.TryGetValue(r.BatchId.Value, out var bn) ? bn : "—",
                Status = r.Status,
                TasksCount = taskCountMap.GetValueOrDefault(r.Id, 0)
            }).ToList();

            // ── Weekly chart data (last 6 weeks by homework sent date) ────────
            var recentHwData = allSentHwData.Where(x => x.CreatedAt >= sixWeeksAgo).ToList();
            var weeklyLabels = new List<string>();
            var weeklyAvgScores = new List<double>();
            var weeklyCompletionRates = new List<double>();
            var weeklyFailingCounts = new List<int>();

            for (int i = 0; i < 6; i++)
            {
                var windowStart = now.Date.AddDays(-42 + i * 7);
                var windowEnd = windowStart.AddDays(7);
                var weekData = recentHwData
                    .Where(x => x.CreatedAt >= windowStart && x.CreatedAt < windowEnd)
                    .ToList();

                weeklyLabels.Add(windowStart.ToString("dd/MM"));
                weeklyAvgScores.Add(weekData.Any(x => x.IsSubmitted && x.Score.HasValue)
                    ? Math.Round(weekData.Where(x => x.IsSubmitted && x.Score.HasValue).Average(x => x.Score!.Value), 1)
                    : 0);
                weeklyCompletionRates.Add(weekData.Any()
                    ? Math.Round(weekData.Count(x => x.IsSubmitted) * 100.0 / weekData.Count, 1)
                    : 0);
                weeklyFailingCounts.Add(weekData.Count(x => x.IsSubmitted && x.Score.HasValue && x.Score.Value < 50));
            }

            // ── Assemble ──────────────────────────────────────────────────────
            var indexModel = new DecisionLabIndexViewModel
            {
                SelectedBatchId = batchId,
                SelectedCurriculumId = curriculumId,
                FromDate = fromDate,
                ToDate = toDate,
                GeneratedAt = now,
                LastUpdatedAt = now,
                Batches = batches,
                TotalStudentsCount = batches.Sum(b => b.ActiveStudentsCount),
                TotalAtRiskCount = criticalCount + highRiskCount,
                OverallAvgScorePct = overallAvgScore,
                HomeworkCompletionPct = hwCompletionPct,
                ApprovedRecommendationsCount = approvedCount,
                PendingRecommendationsCount = pendingCount,
                ActiveInterventionTasksCount = activeTasksCount,
                RiskDistribution = riskDist,
                CriticalIssues = criticalIssues,
                LessonCompletions = lessonCompletions,
                RecentRecommendations = recentRecommendations,
                WeeklyLabels = weeklyLabels,
                WeeklyAvgScores = weeklyAvgScores,
                WeeklyCompletionRates = weeklyCompletionRates,
                WeeklyFailingCounts = weeklyFailingCounts
            };

            if (batchId.HasValue)
            {
                indexModel.SelectedBatchAnalysis = await AnalyzeBatchAsync(
                    batchId.Value,
                    curriculumId,
                    fromDate,
                    toDate);
                indexModel.SelectedBatchName = indexModel.SelectedBatchAnalysis?.BatchName;
            }

            return indexModel;
        }

        public async Task<DecisionLabBatchAnalysisViewModel?> AnalyzeBatchAsync(
            int batchId,
            int? curriculumId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var batchInfo = await LoadBatchInfoAsync(batchId);
            if (batchInfo == null)
            {
                return null;
            }

            var curriculumTitle = await LoadCurriculumTitleAsync(curriculumId);
            var activeStudentsCount = await CountActiveStudentsAsync(batchId);
            var completedLessonsCount = await CountCompletedLessonsAsync(batchId, curriculumId, fromDate, toDate);
            var homeworkRows = await LoadHomeworkRowsAsync(batchId, curriculumId, fromDate, toDate);
            var examRows = await LoadExamRowsAsync(batchId, curriculumId, fromDate, toDate);
            var attendanceRows = await LoadAttendanceRowsAsync(batchId, curriculumId, fromDate, toDate);
            var attemptRows = await LoadQuestionAttemptRowsAsync(batchId, curriculumId, fromDate, toDate);

            var weakLessons = BuildWeakLessons(attemptRows).Take(5).ToList();
            var highRiskQuestions = BuildHighRiskQuestions(attemptRows).Take(10).ToList();

            var sentHomeworkSetsCount = homeworkRows
                .Select(row => row.HomeworkSetId)
                .Distinct()
                .Count();

            var submittedHomeworkCount = homeworkRows.Count(row => row.IsSubmitted);
            var expectedHomeworkSubmissions = sentHomeworkSetsCount * activeStudentsCount;
            var homeworkSubmissionPercent = CalculatePercent(submittedHomeworkCount, expectedHomeworkSubmissions);
            var averageHomeworkScore = AverageNullable(homeworkRows.Select(row => row.Score));

            var sentExamsCount = examRows
                .Select(row => row.ExamAssignmentId)
                .Distinct()
                .Count();

            var submittedExamCount = examRows.Count(row => row.IsSubmitted);
            var expectedExamSubmissions = sentExamsCount * activeStudentsCount;
            var examParticipationPercent = CalculatePercent(submittedExamCount, expectedExamSubmissions);
            var averageExamScore = AverageNullable(examRows.Select(row => row.Score.HasValue ? (double?)row.Score.Value : null));

            var attendancePercent = CalculatePercent(
                attendanceRows.Count(row => row.IsPresent),
                attendanceRows.Count);

            var totalQuestionAttempts = attemptRows.Count;
            var weakLessonsCount = BuildWeakLessons(attemptRows)
                .Count(item => item.ErrorPercentage >= 50);
            var highRiskQuestionsCount = BuildHighRiskQuestions(attemptRows)
                .Count(item => item.ErrorPercentage >= 50);

            var riskBreakdown = BuildRiskBreakdown(
                attendanceRows.Count,
                attendancePercent,
                sentHomeworkSetsCount,
                homeworkSubmissionPercent,
                averageHomeworkScore,
                sentExamsCount,
                examParticipationPercent,
                averageExamScore,
                weakLessons,
                highRiskQuestions);

            var decisionMessage = BuildDecisionMessage(riskBreakdown.OverallRiskLevel, totalQuestionAttempts);

            return new DecisionLabBatchAnalysisViewModel
            {
                BatchId = batchInfo.BatchId,
                BatchName = batchInfo.BatchName,
                CourseId = batchInfo.CourseId,
                CourseName = batchInfo.CourseName,
                CurriculumId = curriculumId,
                CurriculumTitle = curriculumTitle,
                ActiveStudentsCount = activeStudentsCount,
                CompletedLessonsCount = completedLessonsCount,
                TotalQuestionAttempts = totalQuestionAttempts,
                SentHomeworkSetsCount = sentHomeworkSetsCount,
                HomeworkSubmissionPercent = homeworkSubmissionPercent,
                AverageHomeworkScore = averageHomeworkScore,
                SentExamsCount = sentExamsCount,
                ExamParticipationPercent = examParticipationPercent,
                AverageExamScore = averageExamScore,
                AttendancePercent = attendancePercent,
                WeakLessonsCount = weakLessonsCount,
                HighRiskQuestionsCount = highRiskQuestionsCount,
                RiskScore = riskBreakdown.OverallRiskScore,
                RiskLevel = riskBreakdown.OverallRiskLevel,
                DecisionMessage = decisionMessage,
                DecisionSummary = decisionMessage,
                RiskBreakdown = riskBreakdown,
                WeakLessons = weakLessons,
                HighRiskQuestions = highRiskQuestions
            };
        }

        public async Task<IReadOnlyList<DecisionLabWeakLessonViewModel>> GetWeakLessonsAsync(
            int batchId,
            int? curriculumId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var attempts = await LoadQuestionAttemptRowsAsync(batchId, curriculumId, fromDate, toDate);
            return BuildWeakLessons(attempts).Take(5).ToList();
        }

        public async Task<IReadOnlyList<DecisionLabHighRiskQuestionViewModel>> GetHighRiskQuestionsAsync(
            int batchId,
            int? curriculumId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var attempts = await LoadQuestionAttemptRowsAsync(batchId, curriculumId, fromDate, toDate);
            return BuildHighRiskQuestions(attempts).Take(10).ToList();
        }

        private async Task<BatchInfo?> LoadBatchInfoAsync(int batchId)
        {
            return await (
                from batch in _context.Batches.AsNoTracking()
                join course in _context.Courses.AsNoTracking()
                    on batch.CourseId equals course.Id
                where batch.Id == batchId && !batch.IsDeleted
                select new BatchInfo
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    CourseId = course.Id,
                    CourseName = course.Name
                })
                .FirstOrDefaultAsync();
        }

        private async Task<string> LoadCurriculumTitleAsync(int? curriculumId)
        {
            if (!curriculumId.HasValue)
            {
                return string.Empty;
            }

            return await _context.Curriculums
                .AsNoTracking()
                .Where(curriculum => curriculum.Id == curriculumId.Value)
                .Select(curriculum => curriculum.Title)
                .FirstOrDefaultAsync()
                ?? string.Empty;
        }

        private async Task<int> CountActiveStudentsAsync(int batchId)
        {
            return await (
                from enrollment in _context.StudentBatchEnrollments.AsNoTracking()
                join student in _context.Students.AsNoTracking()
                    on enrollment.StudentID equals student.StudentID
                where enrollment.BatchId == batchId
                      && (enrollment.Status == "Active" || enrollment.Status == "نشط")
                      && (student.EnrollmentStatus == "Active" || student.EnrollmentStatus == "نشط")
                select enrollment.StudentID)
                .Distinct()
                .CountAsync();
        }

        private async Task<int> CountCompletedLessonsAsync(
            int batchId,
            int? curriculumId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query =
                from completion in _context.BatchLessonCompletions.AsNoTracking()
                join lesson in _context.Lessons.AsNoTracking()
                    on completion.LessonId equals lesson.Id
                join section in _context.Sections.AsNoTracking()
                    on lesson.SectionId equals section.Id
                where completion.BatchId == batchId
                      && (!curriculumId.HasValue || section.CurriculumId == curriculumId.Value)
                select new
                {
                    completion.LessonId,
                    completion.CompletionDate
                };

            if (fromDate.HasValue)
            {
                query = query.Where(row => row.CompletionDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(row => row.CompletionDate <= toDate.Value);
            }

            return await query
                .Select(row => row.LessonId)
                .Distinct()
                .CountAsync();
        }

        private async Task<List<HomeworkRow>> LoadHomeworkRowsAsync(
            int batchId,
            int? curriculumId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query =
                from homeworkStudent in _context.HomeworkSetStudents.AsNoTracking()
                join homeworkSet in _context.HomeworkSets.AsNoTracking()
                    on homeworkStudent.HomeworkSetId equals homeworkSet.Id
                where homeworkSet.BatchId == batchId
                      && homeworkSet.IsSent
                      && (!curriculumId.HasValue || homeworkSet.CurriculumId == curriculumId.Value)
                select new HomeworkRow
                {
                    HomeworkSetId = homeworkSet.Id,
                    StudentId = homeworkStudent.StudentId,
                    IsSubmitted = homeworkStudent.IsSubmitted,
                    SubmittedAt = homeworkStudent.SubmittedAt,
                    Score = homeworkStudent.Score,
                    CreatedAt = homeworkSet.CreatedAt
                };

            if (fromDate.HasValue)
            {
                query = query.Where(row => row.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(row => row.CreatedAt <= toDate.Value);
            }

            return await query.ToListAsync();
        }

        private async Task<List<ExamRow>> LoadExamRowsAsync(
            int batchId,
            int? curriculumId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query =
                from status in _context.ExamStudentStatuses.AsNoTracking()
                join assignment in _context.ExamAssignmentsToBatches.AsNoTracking()
                    on status.ExamAssignmentId equals (int?)assignment.Id
                where assignment.BatchId == batchId
                      && assignment.IsSentToStudents
                      && (!curriculumId.HasValue || assignment.CurriculumId == curriculumId.Value)
                select new ExamRow
                {
                    ExamAssignmentId = assignment.Id,
                    StudentId = status.StudentId,
                    IsSubmitted = status.IsSubmitted,
                    Score = status.Score,
                    AssignedAt = status.AssignedAt,
                    AssignmentCreatedAt = assignment.CreatedAt
                };

            if (fromDate.HasValue)
            {
                query = query.Where(row => row.AssignmentCreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(row => row.AssignmentCreatedAt <= toDate.Value);
            }

            return await query.ToListAsync();
        }

        private async Task<List<AttendanceRow>> LoadAttendanceRowsAsync(
            int batchId,
            int? curriculumId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query =
                from attendance in _context.AttendanceRecords.AsNoTracking()
                join lecture in _context.Lecture.AsNoTracking()
                    on attendance.LectureId equals lecture.Id
                join section in _context.Sections.AsNoTracking()
                    on lecture.SectionId equals section.Id
                where lecture.BatchId == batchId
                      && (!curriculumId.HasValue || section.CurriculumId == curriculumId.Value)
                select new AttendanceRow
                {
                    StudentId = attendance.StudentId,
                    LectureId = attendance.LectureId,
                    IsPresent = attendance.IsPresent,
                    RecordedAt = attendance.RecordedAt
                };

            if (fromDate.HasValue)
            {
                query = query.Where(row => row.RecordedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(row => row.RecordedAt <= toDate.Value);
            }

            return await query.ToListAsync();
        }

        private async Task<List<QuestionAttemptRow>> LoadQuestionAttemptRowsAsync(
            int batchId,
            int? curriculumId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query =
                from attempt in _context.QuestionAttemptNew.AsNoTracking()
                join enrollment in _context.StudentBatchEnrollments.AsNoTracking()
                    on attempt.StudentId equals enrollment.StudentID
                join question in _context.Questions.AsNoTracking()
                    on attempt.QuestionId equals question.Id
                join lesson in _context.Lessons.AsNoTracking()
                    on question.LessonId equals lesson.Id
                join section in _context.Sections.AsNoTracking()
                    on lesson.SectionId equals section.Id
                where enrollment.BatchId == batchId
                      && (enrollment.Status == "Active" || enrollment.Status == "نشط")
                      && (!curriculumId.HasValue || section.CurriculumId == curriculumId.Value)
                select new QuestionAttemptRow
                {
                    StudentId = attempt.StudentId,
                    QuestionId = question.Id,
                    QuestionTitle = question.Title ?? string.Empty,
                    ReferenceNumber = question.ReferenceNumber ?? string.Empty,
                    LessonId = lesson.Id,
                    LessonTitle = lesson.Title,
                    SectionId = section.Id,
                    SectionTitle = section.Title,
                    IsCorrect = attempt.IsCorrect,
                    TimeTakenSeconds = attempt.TimeTakenSeconds,
                    AttemptedAt = attempt.AttemptedAt
                };

            if (fromDate.HasValue)
            {
                query = query.Where(row => row.AttemptedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(row => row.AttemptedAt <= toDate.Value);
            }

            return await query.ToListAsync();
        }

        private static List<DecisionLabWeakLessonViewModel> BuildWeakLessons(
            IReadOnlyCollection<QuestionAttemptRow> attempts)
        {
            return attempts
                .GroupBy(row => new
                {
                    row.LessonId,
                    row.LessonTitle,
                    row.SectionId,
                    row.SectionTitle
                })
                .Select(group =>
                {
                    var total = group.Count();
                    var wrong = group.Count(row => !row.IsCorrect);
                    var correct = total - wrong;
                    var errorPercentage = CalculatePercent(wrong, total);

                    return new DecisionLabWeakLessonViewModel
                    {
                        LessonId = group.Key.LessonId,
                        LessonTitle = group.Key.LessonTitle,
                        SectionId = group.Key.SectionId,
                        SectionTitle = group.Key.SectionTitle,
                        TotalAttempts = total,
                        CorrectAttempts = correct,
                        WrongAttempts = wrong,
                        AffectedStudentsCount = group.Select(row => row.StudentId).Distinct().Count(),
                        ErrorPercentage = errorPercentage,
                        AverageTimeTakenSeconds = Math.Round(group.Average(row => row.TimeTakenSeconds), 1),
                        RelatedQuestionsCount = group.Select(row => row.QuestionId).Distinct().Count(),
                        RiskLevel = MapRiskLevel(errorPercentage)
                    };
                })
                .OrderByDescending(item => item.ErrorPercentage)
                .ThenByDescending(item => item.TotalAttempts)
                .ToList();
        }

        private static List<DecisionLabHighRiskQuestionViewModel> BuildHighRiskQuestions(
            IReadOnlyCollection<QuestionAttemptRow> attempts)
        {
            return attempts
                .GroupBy(row => new
                {
                    row.QuestionId,
                    row.ReferenceNumber,
                    row.QuestionTitle,
                    row.LessonId,
                    row.LessonTitle,
                    row.SectionId,
                    row.SectionTitle
                })
                .Select(group =>
                {
                    var total = group.Count();
                    var wrong = group.Count(row => !row.IsCorrect);
                    var correct = total - wrong;
                    var errorPercentage = CalculatePercent(wrong, total);

                    return new DecisionLabHighRiskQuestionViewModel
                    {
                        QuestionId = group.Key.QuestionId,
                        ReferenceNumber = group.Key.ReferenceNumber,
                        QuestionTitle = group.Key.QuestionTitle,
                        LessonId = group.Key.LessonId,
                        LessonTitle = group.Key.LessonTitle,
                        SectionId = group.Key.SectionId,
                        SectionTitle = group.Key.SectionTitle,
                        TotalAttempts = total,
                        CorrectAttempts = correct,
                        WrongAttempts = wrong,
                        AffectedStudentsCount = group.Select(row => row.StudentId).Distinct().Count(),
                        ErrorPercentage = errorPercentage,
                        AverageTimeTakenSeconds = Math.Round(group.Average(row => row.TimeTakenSeconds), 1),
                        RiskLevel = MapRiskLevel(errorPercentage)
                    };
                })
                .OrderByDescending(item => item.ErrorPercentage)
                .ThenByDescending(item => item.TotalAttempts)
                .ToList();
        }

        private static DecisionRiskBreakdownViewModel BuildRiskBreakdown(
            int attendanceRowsCount,
            double attendancePercent,
            int sentHomeworkSetsCount,
            double homeworkSubmissionPercent,
            double averageHomeworkScore,
            int sentExamsCount,
            double examParticipationPercent,
            double averageExamScore,
            IReadOnlyCollection<DecisionLabWeakLessonViewModel> weakLessons,
            IReadOnlyCollection<DecisionLabHighRiskQuestionViewModel> highRiskQuestions)
        {
            double? attendanceRisk = attendanceRowsCount > 0
                ? 100 - attendancePercent
                : null;

            double? homeworkRisk = sentHomeworkSetsCount > 0
                ? 100 - (averageHomeworkScore > 0 ? averageHomeworkScore : homeworkSubmissionPercent)
                : null;

            double? examRisk = sentExamsCount > 0
                ? 100 - (averageExamScore > 0 ? averageExamScore : examParticipationPercent)
                : null;

            double? weakLessonRisk = weakLessons.Any()
                ? weakLessons.Average(item => item.ErrorPercentage)
                : null;

            double? questionRisk = highRiskQuestions.Any()
                ? highRiskQuestions.Average(item => item.ErrorPercentage)
                : null;

            var overall = AverageAvailable(
                attendanceRisk,
                homeworkRisk,
                examRisk,
                weakLessonRisk,
                questionRisk);

            return new DecisionRiskBreakdownViewModel
            {
                OverallRiskScore = overall,
                OverallRiskLevel = MapRiskLevel(overall),
                AttendanceRiskScore = attendanceRisk ?? 0,
                AttendanceSignal = attendanceRowsCount > 0
                    ? $"متوسط الحضور {attendancePercent:0.#}%"
                    : "لا توجد سجلات حضور كافية",
                HomeworkRiskScore = homeworkRisk ?? 0,
                HomeworkSignal = sentHomeworkSetsCount > 0
                    ? $"متوسط الواجبات {averageHomeworkScore:0.#}% ونسبة التسليم {homeworkSubmissionPercent:0.#}%"
                    : "لا توجد بيانات واجبات مرسلة",
                ExamRiskScore = examRisk ?? 0,
                ExamSignal = sentExamsCount > 0
                    ? $"متوسط الاختبارات {averageExamScore:0.#}% ونسبة المشاركة {examParticipationPercent:0.#}%"
                    : "لا توجد بيانات اختبارات مرسلة",
                WeakLessonRiskScore = weakLessonRisk ?? 0,
                WeakLessonSignal = weakLessons.Any()
                    ? $"أعلى متوسط خطأ في المؤشرات {weakLessons.First().ErrorPercentage:0.#}%"
                    : "لا توجد محاولات كافية لتحليل المؤشرات",
                QuestionRiskScore = questionRisk ?? 0,
                QuestionSignal = highRiskQuestions.Any()
                    ? $"أعلى نسبة خطأ في سؤال {highRiskQuestions.First().ErrorPercentage:0.#}%"
                    : "لا توجد محاولات كافية لتحليل الأسئلة"
            };
        }

        private static string BuildDecisionMessage(string riskLevel, int totalQuestionAttempts)
        {
            if (totalQuestionAttempts == 0)
            {
                return "لا توجد محاولات أسئلة كافية لهذه الدفعة؛ القرار المناسب الآن هو متابعة إدخال البيانات قبل إصدار توصية تشغيلية.";
            }

            return riskLevel switch
            {
                "حرج" => "الدفعة تحتاج تدخلاً عاجلاً: راجع الحضور، الواجبات، وأضعف المؤشرات قبل إرسال اختبار جديد.",
                "مرتفع" => "يوجد ارتفاع واضح في مؤشرات الخطر؛ يفضل تنفيذ مراجعة مركزة على المؤشرات والأسئلة الأكثر خطأ.",
                "متوسط" => "أداء الدفعة متوسط ويحتاج متابعة قريبة، مع تحسين الالتزام بالواجبات والحضور إن ظهرت إشارات ضعف.",
                _ => "مؤشرات الدفعة مستقرة حاليًا، والمتابعة الدورية كافية ما لم تظهر بيانات ضعف جديدة."
            };
        }

        private static double CalculatePercent(int value, int total)
        {
            return total <= 0
                ? 0
                : Math.Round(value * 100.0 / total, 1);
        }

        private static double AverageNullable(IEnumerable<double?> values)
        {
            var availableValues = values
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToList();

            return availableValues.Count == 0
                ? 0
                : Math.Round(availableValues.Average(), 1);
        }

        private static double AverageAvailable(params double?[] values)
        {
            var availableValues = values
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToList();

            return availableValues.Count == 0
                ? 0
                : Math.Round(availableValues.Average(), 1);
        }

        private static string MapRiskLevel(double score)
        {
            if (score >= 75)
            {
                return "حرج";
            }

            if (score >= 55)
            {
                return "مرتفع";
            }

            if (score >= 30)
            {
                return "متوسط";
            }

            return "منخفض";
        }

        private sealed class BatchInfo
        {
            public int BatchId { get; set; }
            public string BatchName { get; set; } = string.Empty;
            public int CourseId { get; set; }
            public string CourseName { get; set; } = string.Empty;
        }

        private sealed class HomeworkRow
        {
            public int HomeworkSetId { get; set; }
            public int StudentId { get; set; }
            public bool IsSubmitted { get; set; }
            public DateTime? SubmittedAt { get; set; }
            public double? Score { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private sealed class ExamRow
        {
            public int ExamAssignmentId { get; set; }
            public int StudentId { get; set; }
            public bool IsSubmitted { get; set; }
            public int? Score { get; set; }
            public DateTime AssignedAt { get; set; }
            public DateTime AssignmentCreatedAt { get; set; }
        }

        private sealed class AttendanceRow
        {
            public int StudentId { get; set; }
            public int LectureId { get; set; }
            public bool IsPresent { get; set; }
            public DateTime RecordedAt { get; set; }
        }

        private sealed class QuestionAttemptRow
        {
            public int StudentId { get; set; }
            public Guid QuestionId { get; set; }
            public string ReferenceNumber { get; set; } = string.Empty;
            public string QuestionTitle { get; set; } = string.Empty;
            public int LessonId { get; set; }
            public string LessonTitle { get; set; } = string.Empty;
            public int SectionId { get; set; }
            public string SectionTitle { get; set; } = string.Empty;
            public bool IsCorrect { get; set; }
            public double TimeTakenSeconds { get; set; }
            public DateTime AttemptedAt { get; set; }
        }
    }
}
