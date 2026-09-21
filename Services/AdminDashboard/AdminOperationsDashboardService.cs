using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.ViewModels.Dashboard;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.AdminDashboard
{
    public class AdminOperationsDashboardService : IAdminOperationsDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IAdminLiveStudentTracker _liveStudentTracker;

        private const int LowAttendanceThreshold = 70;
        private const int CriticalAttendanceThreshold = 50;
        private const int DefaultLectureDurationMinutes = 120;
        private const int LiveLectureAttendanceWindowMinutes = 30;

        public AdminOperationsDashboardService(
            ApplicationDbContext context,
            IMemoryCache cache,
            IAdminLiveStudentTracker liveStudentTracker)
        {
            _context = context;
            _cache = cache;
            _liveStudentTracker = liveStudentTracker;
        }

        public async Task<AdminOperationsDashboardViewModel> GetDashboardAsync()
        {
            DateTime now = DateTime.Now;

            string cacheKey =
                "admin-operations-dashboard-decision-room-full-"
                + now.ToString("yyyyMMddHHmm")
                + "-"
                + (now.Second / 10).ToString();

            if (_cache.TryGetValue(cacheKey, out AdminOperationsDashboardViewModel cachedModel))
            {
                return cachedModel;
            }

            DateTime todayStart = now.Date;
            DateTime tomorrowStart = todayStart.AddDays(1);
            DateTime yesterdayStart = todayStart.AddDays(-1);
            DateTime weekStart = todayStart.AddDays(-6);
            DateTime nextSevenDaysEnd = todayStart.AddDays(7);
            DateTime within24Hours = now.AddHours(24);
            DateTime last30DaysStart = todayStart.AddDays(-30);

            List<LiveStudentSnapshot> liveStudentSnapshots = _liveStudentTracker.GetSnapshots();

            List<BatchBasicProjection> batches = await LoadBatchesAsync(last30DaysStart);
            List<StudentBatchProjection> studentBatches = await LoadStudentBatchesAsync();

            List<LectureProjection> todayLectures = await LoadLecturesAsync(todayStart, tomorrowStart);
            List<LectureProjection> yesterdayLectures = await LoadLecturesAsync(yesterdayStart, todayStart);
            List<LectureProjection> weekLectures = await LoadLecturesAsync(weekStart, tomorrowStart);

            List<AttendanceProjection> todayAttendance = await LoadAttendanceAsync(todayStart, tomorrowStart);
            List<AttendanceProjection> yesterdayAttendance = await LoadAttendanceAsync(yesterdayStart, todayStart);

            List<HomeworkSetProjection> weekHomeworkSets = await LoadWeekHomeworkSetsAsync(weekStart, tomorrowStart);
            List<HomeworkSetStudentProjection> homeworkStudentRows = await LoadHomeworkStudentRowsAsync(weekStart, tomorrowStart);

            List<ExamAssignmentProjection> weekExamAssignments = await LoadWeekExamAssignmentsAsync(weekStart, tomorrowStart);
            List<ExamStatusProjection> examStatusRows = await LoadExamStatusRowsAsync(weekStart, tomorrowStart);

            List<PredictiveLectureProjection> predictiveLectures = await LoadPredictiveLecturesAsync(todayStart, nextSevenDaysEnd);
            List<PredictiveExamProjection> predictiveExams = await LoadPredictiveExamsAsync(todayStart, nextSevenDaysEnd);
            List<PredictiveHomeworkProjection> predictiveHomeworks = await LoadPredictiveHomeworksAsync(todayStart, nextSevenDaysEnd);

            AdminOperationsDashboardViewModel model = new AdminOperationsDashboardViewModel
            {
                Kpis = BuildKpis(
                    now,
                    todayStart,
                    tomorrowStart,
                    yesterdayStart,
                    within24Hours,
                    studentBatches,
                    todayLectures,
                    yesterdayLectures,
                    todayAttendance,
                    yesterdayAttendance,
                    predictiveExams,
                    predictiveHomeworks,
                    liveStudentSnapshots),

                LectureTimeline = BuildLectureTimeline(now, todayLectures, todayAttendance, studentBatches),

                LiveAttendance = BuildLiveAttendance(now, todayLectures, todayAttendance, studentBatches),

                BatchPerformanceMatrix = BuildBatchPerformanceMatrix(
                    batches,
                    studentBatches,
                    todayAttendance,
                    homeworkStudentRows,
                    examStatusRows),

                InstructorPerformance = BuildInstructorPerformance(
                    weekLectures,
                    weekHomeworkSets,
                    weekExamAssignments,
                    todayAttendance,
                    studentBatches),

                PredictiveCalendar = BuildPredictiveCalendar(
                    todayStart,
                    predictiveLectures,
                    predictiveExams,
                    predictiveHomeworks),

                GeneratedAt = now
            };

            model.SmartAlerts = BuildSmartAlerts(model);

            _cache.Set(
                cacheKey,
                model,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
                });

            return model;
        }

        public async Task<AdminDashboardPulseViewModel> GetPulseAsync()
        {
            DateTime now = DateTime.Now;

            string cacheKey =
                "admin-operations-dashboard-decision-room-pulse-"
                + now.ToString("yyyyMMddHHmmss");

            if (_cache.TryGetValue(cacheKey, out AdminDashboardPulseViewModel cachedModel))
            {
                return cachedModel;
            }

            DateTime todayStart = now.Date;
            DateTime tomorrowStart = todayStart.AddDays(1);
            DateTime yesterdayStart = todayStart.AddDays(-1);
            DateTime within24Hours = now.AddHours(24);

            List<LiveStudentSnapshot> liveStudentSnapshots = _liveStudentTracker.GetSnapshots();

            List<StudentBatchProjection> studentBatches = await LoadStudentBatchesAsync();

            List<LectureProjection> todayLectures = await LoadLecturesAsync(todayStart, tomorrowStart);
            List<LectureProjection> yesterdayLectures = await LoadLecturesAsync(yesterdayStart, todayStart);

            List<AttendanceProjection> todayAttendance = await LoadAttendanceAsync(todayStart, tomorrowStart);
            List<AttendanceProjection> yesterdayAttendance = await LoadAttendanceAsync(yesterdayStart, todayStart);

            List<PredictiveExamProjection> predictiveExams = await LoadPredictiveExamsAsync(todayStart, tomorrowStart);
            List<PredictiveHomeworkProjection> predictiveHomeworks = await LoadPredictiveHomeworksAsync(todayStart, within24Hours);

            AdminDashboardKpiViewModel kpis = BuildKpis(
                now,
                todayStart,
                tomorrowStart,
                yesterdayStart,
                within24Hours,
                studentBatches,
                todayLectures,
                yesterdayLectures,
                todayAttendance,
                yesterdayAttendance,
                predictiveExams,
                predictiveHomeworks,
                liveStudentSnapshots);

            List<LiveAttendanceRowViewModel> liveAttendance = BuildLiveAttendance(
                now,
                todayLectures,
                todayAttendance,
                studentBatches);

            AdminDashboardPulseViewModel model = new AdminDashboardPulseViewModel
            {
                Kpis = kpis,
                LiveAttendance = liveAttendance,
                SmartAlerts = BuildPulseAlerts(kpis, liveAttendance),
                GeneratedAt = now
            };

            _cache.Set(
                cacheKey,
                model,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
                });

            return model;
        }

        private async Task<List<BatchBasicProjection>> LoadBatchesAsync(DateTime last30DaysStart)
        {
            /* Step 1 — batch IDs that had at least one lecture in the last 30 days */
            List<int> recentIds = await _context.Lecture
                .AsNoTracking()
                .Where(l => l.Date >= last30DaysStart)
                .Select(l => l.BatchId)
                .Distinct()
                .ToListAsync();

            HashSet<int> recentSet = new HashSet<int>(recentIds);

            /* Step 2 — non-archived, non-deleted batches with recent activity */
            return await _context.Batches
                .AsNoTracking()
                .Where(batch =>
                    !batch.IsDeleted &&
                    !batch.IsArchived &&
                    recentSet.Contains(batch.Id))
                .Select(batch => new BatchBasicProjection
                {
                    BatchId   = batch.Id,
                    BatchName = batch.Name,
                    IsPartner = batch.Branch != null && batch.Branch.IsPartner
                })
                .ToListAsync();
        }

        private async Task<List<StudentBatchProjection>> LoadStudentBatchesAsync()
        {
            return await (
                from enrollment in _context.StudentBatchEnrollments.AsNoTracking()
                join student in _context.Students.AsNoTracking()
                    on enrollment.StudentID equals student.StudentID
                select new StudentBatchProjection
                {
                    StudentId = student.StudentID,
                    BatchId = enrollment.BatchId,
                    IsActive =
                        (student.EnrollmentStatus == "نشط" || student.EnrollmentStatus == "Active")
                        && (enrollment.Status == "Active" || enrollment.Status == "نشط"),
                    LastLoginAt = student.LastLoginAt
                })
                .ToListAsync();
        }

        private async Task<List<LectureProjection>> LoadLecturesAsync(DateTime startDate, DateTime endDate)
        {
            return await (
                from lecture in _context.Lecture.AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on lecture.BatchId equals batch.Id
                join instructor in _context.Instructors.AsNoTracking()
                    on lecture.InstructorId equals instructor.Id
                where lecture.Date >= startDate
                      && lecture.Date < endDate
                select new LectureProjection
                {
                    LectureId = lecture.Id,
                    Title = lecture.Title,
                    BatchId = lecture.BatchId,
                    BatchName = batch.Name,
                    InstructorId = lecture.InstructorId,
                    InstructorName = instructor.FullName,
                    StartAt = lecture.Date,
                    EndAt = lecture.Date.AddMinutes(DefaultLectureDurationMinutes)
                })
                .OrderBy(item => item.StartAt)
                .ToListAsync();
        }

        private async Task<List<AttendanceProjection>> LoadAttendanceAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.AttendanceRecords
                .AsNoTracking()
                .Where(record => record.RecordedAt >= startDate && record.RecordedAt < endDate)
                .Select(record => new AttendanceProjection
                {
                    LectureId = record.LectureId,
                    StudentId = record.StudentId,
                    IsPresent = record.IsPresent,
                    RecordedAt = record.RecordedAt
                })
                .ToListAsync();
        }

        private async Task<List<HomeworkSetProjection>> LoadWeekHomeworkSetsAsync(DateTime weekStart, DateTime tomorrowStart)
        {
            return await (
                from homeworkSet in _context.HomeworkSets.AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on homeworkSet.BatchId equals batch.Id
                where homeworkSet.CreatedAt >= weekStart
                      && homeworkSet.CreatedAt < tomorrowStart
                select new HomeworkSetProjection
                {
                    HomeworkSetId = homeworkSet.Id,
                    BatchId = homeworkSet.BatchId,
                    BatchName = batch.Name,
                    CreatedAt = homeworkSet.CreatedAt,
                    EndAt = homeworkSet.EndAt,
                    IsSent = homeworkSet.IsSent,
                    AssignedByUserId = homeworkSet.AssignedByUserId
                })
                .ToListAsync();
        }

        private async Task<List<HomeworkSetStudentProjection>> LoadHomeworkStudentRowsAsync(DateTime weekStart, DateTime tomorrowStart)
        {
            return await (
                from homeworkSetStudent in _context.HomeworkSetStudents.AsNoTracking()
                join homeworkSet in _context.HomeworkSets.AsNoTracking()
                    on homeworkSetStudent.HomeworkSetId equals homeworkSet.Id
                where homeworkSet.CreatedAt >= weekStart
                      && homeworkSet.CreatedAt < tomorrowStart
                select new HomeworkSetStudentProjection
                {
                    HomeworkSetId = homeworkSet.Id,
                    BatchId = homeworkSet.BatchId,
                    StudentId = homeworkSetStudent.StudentId,
                    IsSubmitted = homeworkSetStudent.IsSubmitted,
                    SubmittedAt = homeworkSetStudent.SubmittedAt,
                    Score = homeworkSetStudent.Score
                })
                .ToListAsync();
        }

        private async Task<List<ExamAssignmentProjection>> LoadWeekExamAssignmentsAsync(DateTime weekStart, DateTime tomorrowStart)
        {
            return await (
                from assignment in _context.ExamAssignmentsToBatches.AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on assignment.BatchId equals batch.Id
                where assignment.CreatedAt >= weekStart
                      && assignment.CreatedAt < tomorrowStart
                select new ExamAssignmentProjection
                {
                    ExamAssignmentId = assignment.Id,
                    BatchId = assignment.BatchId,
                    BatchName = batch.Name,
                    CreatedAt = assignment.CreatedAt,
                    ScheduledDate = assignment.ScheduledDate,
                    DurationMinutes = assignment.DurationMinutes,
                    IsSentToStudents = assignment.IsSentToStudents,
                    CreatedByInstructorId = assignment.CreatedByInstructorId
                })
                .ToListAsync();
        }

        private async Task<List<ExamStatusProjection>> LoadExamStatusRowsAsync(DateTime weekStart, DateTime tomorrowStart)
        {
            return await (
                from status in _context.ExamStudentStatuses.AsNoTracking()
                join assignment in _context.ExamAssignmentsToBatches.AsNoTracking()
                    on status.ExamAssignmentId equals assignment.Id
                where assignment.CreatedAt >= weekStart
                      && assignment.CreatedAt < tomorrowStart
                select new ExamStatusProjection
                {
                    ExamAssignmentId = assignment.Id,
                    BatchId = assignment.BatchId,
                    StudentId = status.StudentId,
                    IsSubmitted = status.IsSubmitted,
                    Score = status.Score,
                    AssignedAt = status.AssignedAt,
                    SubmittedAt = status.SubmittedAt
                })
                .ToListAsync();
        }

        private async Task<List<PredictiveLectureProjection>> LoadPredictiveLecturesAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Lecture
                .AsNoTracking()
                .Where(lecture => lecture.Date >= startDate && lecture.Date < endDate)
                .Select(lecture => new PredictiveLectureProjection
                {
                    LectureId = lecture.Id,
                    Date = lecture.Date.Date,
                    StartAt = lecture.Date,
                    EndAt = lecture.Date.AddMinutes(DefaultLectureDurationMinutes),
                    BatchId = lecture.BatchId
                })
                .ToListAsync();
        }

        private async Task<List<PredictiveExamProjection>> LoadPredictiveExamsAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Where(assignment => assignment.ScheduledDate.HasValue
                                     && assignment.ScheduledDate.Value >= startDate
                                     && assignment.ScheduledDate.Value < endDate
                                     && assignment.IsSentToStudents)
                .Select(assignment => new PredictiveExamProjection
                {
                    ExamAssignmentId = assignment.Id,
                    BatchId = assignment.BatchId,
                    ScheduledDate = assignment.ScheduledDate.Value,
                    DurationMinutes = assignment.DurationMinutes,
                    IsSentToStudents = assignment.IsSentToStudents
                })
                .ToListAsync();
        }

        private async Task<List<PredictiveHomeworkProjection>> LoadPredictiveHomeworksAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.HomeworkSets
                .AsNoTracking()
                .Where(homeworkSet => homeworkSet.EndAt.HasValue
                                      && homeworkSet.EndAt.Value >= startDate
                                      && homeworkSet.EndAt.Value < endDate
                                      && homeworkSet.IsSent)
                .Select(homeworkSet => new PredictiveHomeworkProjection
                {
                    HomeworkSetId = homeworkSet.Id,
                    BatchId = homeworkSet.BatchId,
                    EndAt = homeworkSet.EndAt.Value,
                    IsSent = homeworkSet.IsSent
                })
                .ToListAsync();
        }

        private AdminDashboardKpiViewModel BuildKpis(
            DateTime now,
            DateTime todayStart,
            DateTime tomorrowStart,
            DateTime yesterdayStart,
            DateTime within24Hours,
            List<StudentBatchProjection> studentBatches,
            List<LectureProjection> todayLectures,
            List<LectureProjection> yesterdayLectures,
            List<AttendanceProjection> todayAttendance,
            List<AttendanceProjection> yesterdayAttendance,
            List<PredictiveExamProjection> predictiveExams,
            List<PredictiveHomeworkProjection> predictiveHomeworks,
            List<LiveStudentSnapshot> liveStudentSnapshots)
        {
            Dictionary<int, bool> activeStudentMap = new Dictionary<int, bool>();

            foreach (LiveStudentSnapshot snapshot in liveStudentSnapshots)
            {
                if (snapshot.IsLiveNow && !activeStudentMap.ContainsKey(snapshot.StudentId))
                {
                    activeStudentMap.Add(snapshot.StudentId, true);
                }
            }

            foreach (StudentBatchProjection student in studentBatches)
            {
                if (student.IsActive
                    && student.LastLoginAt.HasValue
                    && student.LastLoginAt.Value >= todayStart
                    && student.LastLoginAt.Value < tomorrowStart
                    && !activeStudentMap.ContainsKey(student.StudentId))
                {
                    activeStudentMap.Add(student.StudentId, true);
                }
            }

            int activeStudentsToday = activeStudentMap.Count;

            int activeStudentsYesterday = studentBatches
                .Where(item => item.IsActive
                               && item.LastLoginAt.HasValue
                               && item.LastLoginAt.Value >= yesterdayStart
                               && item.LastLoginAt.Value < todayStart)
                .Select(item => item.StudentId)
                .Distinct()
                .Count();

            int runningLecturesNow = todayLectures.Count(item =>
                IsLectureRunningNow(now, item, todayAttendance));
            int runningLecturesYesterday = yesterdayLectures.Count();

            int openExamsNow = predictiveExams
                .Count(item => item.ScheduledDate <= now
                               && item.ScheduledDate.AddMinutes(item.DurationMinutes) >= now);

            int openExamsYesterday = predictiveExams
                .Count(item => item.ScheduledDate.Date == yesterdayStart.Date);

            int presentCountToday = todayAttendance.Count(item => item.IsPresent);
            int attendanceTotalCountToday = todayAttendance.Count;
            double attendancePercentToday = CalculatePercent(presentCountToday, attendanceTotalCountToday);

            int presentCountYesterday = yesterdayAttendance.Count(item => item.IsPresent);
            int attendanceTotalCountYesterday = yesterdayAttendance.Count;
            double attendancePercentYesterday = CalculatePercent(presentCountYesterday, attendanceTotalCountYesterday);

            int homeworksClosingWithin24Hours = predictiveHomeworks
                .Count(item => item.EndAt >= now && item.EndAt <= within24Hours);

            int homeworksClosingYesterday = predictiveHomeworks
                .Count(item => item.EndAt.Date == yesterdayStart.Date);

            int completedLecturesToday = todayLectures.Count(item => item.EndAt < now);

            int lowAttendanceLecturesToday = CountLowAttendanceLectures(
                now,
                todayLectures,
                todayAttendance,
                studentBatches);

            int allActiveStudentsCount = studentBatches
                .Where(item => item.IsActive)
                .Select(item => item.StudentId)
                .Distinct()
                .Count();

            int presentStudentsToday = todayAttendance
                .Where(item => item.IsPresent)
                .Select(item => item.StudentId)
                .Distinct()
                .Count();

            int absentAllDay = allActiveStudentsCount - presentStudentsToday;

            if (absentAllDay < 0)
            {
                absentAllDay = 0;
            }

            return new AdminDashboardKpiViewModel
            {
                ActiveStudentsToday = activeStudentsToday,
                ActiveStudentsYesterday = activeStudentsYesterday,
                RunningLecturesNow = runningLecturesNow,
                RunningLecturesYesterday = runningLecturesYesterday,
                OpenExamsNow = openExamsNow,
                OpenExamsYesterday = openExamsYesterday,
                AttendancePercentToday = attendancePercentToday,
                AttendancePercentYesterday = attendancePercentYesterday,
                HomeworksClosingWithin24Hours = homeworksClosingWithin24Hours,
                HomeworksClosingYesterday = homeworksClosingYesterday,
                CompletedLecturesToday = completedLecturesToday,
                LowAttendanceLecturesToday = lowAttendanceLecturesToday,
                AbsentAllDayStudentsCount = absentAllDay,
                ActiveStudentsTrendText = BuildTrendText(activeStudentsToday, activeStudentsYesterday, "طالب"),
                AttendanceTrendText = BuildTrendText(attendancePercentToday, attendancePercentYesterday, "%"),
                RunningLecturesTrendText = BuildTrendText(runningLecturesNow, runningLecturesYesterday, "محاضرة"),
                OpenExamsTrendText = BuildTrendText(openExamsNow, openExamsYesterday, "اختبار"),
                HomeworksClosingTrendText = BuildTrendText(homeworksClosingWithin24Hours, homeworksClosingYesterday, "واجب")
            };
        }

        private List<LectureTimelineItemViewModel> BuildLectureTimeline(
            DateTime now,
            List<LectureProjection> todayLectures,
            List<AttendanceProjection> todayAttendance,
            List<StudentBatchProjection> studentBatches)
        {
            List<LectureTimelineItemViewModel> result = new List<LectureTimelineItemViewModel>();

            foreach (LectureProjection lecture in todayLectures.OrderBy(item => item.StartAt))
            {
                int totalStudents = studentBatches.Count(item => item.BatchId == lecture.BatchId && item.IsActive);

                int presentStudents = todayAttendance
                    .Where(item => item.LectureId == lecture.LectureId && item.IsPresent)
                    .Select(item => item.StudentId)
                    .Distinct()
                    .Count();

                double attendancePercent = CalculatePercent(presentStudents, totalStudents);
                string statusCode = ResolveLectureStatusCode(now, lecture, attendancePercent);
                string statusText = ResolveLectureStatusText(statusCode);

                int startMinute = (lecture.StartAt.Hour * 60) + lecture.StartAt.Minute;
                int duration = Convert.ToInt32(Math.Max(1, (lecture.EndAt - lecture.StartAt).TotalMinutes));

                result.Add(new LectureTimelineItemViewModel
                {
                    LectureId = lecture.LectureId,
                    Title = lecture.Title,
                    BatchName = lecture.BatchName,
                    InstructorName = lecture.InstructorName,
                    StartAt = lecture.StartAt,
                    EndAt = lecture.EndAt,
                    StartMinuteOfDay = startMinute,
                    DurationMinutes = duration,
                    StatusCode = statusCode,
                    StatusText = statusText,
                    AttendancePercent = attendancePercent,
                    PresentStudents = presentStudents,
                    TotalStudents = totalStudents,
                    IsUrgent = attendancePercent < CriticalAttendanceThreshold && lecture.StartAt <= now
                });
            }

            return result;
        }

        private List<LiveAttendanceRowViewModel> BuildLiveAttendance(
           DateTime now,
           List<LectureProjection> todayLectures,
           List<AttendanceProjection> todayAttendance,
           List<StudentBatchProjection> studentBatches)
        {
            List<LectureProjection> runningOrNearLectures = todayLectures
                .Where(item =>
                    IsLectureRunningNow(now, item, todayAttendance)
                    || (item.StartAt <= now.AddMinutes(15) && item.EndAt >= now.AddMinutes(-15)))
                .OrderBy(item => item.StartAt)
                .ToList();

            List<LiveAttendanceRowViewModel> result = new List<LiveAttendanceRowViewModel>();

            foreach (LectureProjection lecture in runningOrNearLectures)
            {
                int totalStudents = studentBatches.Count(item =>
                    item.BatchId == lecture.BatchId &&
                    item.IsActive);

                int connectedStudents = todayAttendance
                    .Where(item => item.LectureId == lecture.LectureId && item.IsPresent)
                    .Select(item => item.StudentId)
                    .Distinct()
                    .Count();

                double attendancePercent = CalculatePercent(connectedStudents, totalStudents);

                bool isRunningNow = IsLectureRunningNow(now, lecture, todayAttendance);

                string statusText = ResolveLiveLectureStatusText(
                    now,
                    lecture,
                    todayAttendance,
                    attendancePercent);

                bool canWarnInstructor =
                    isRunningNow &&
                    totalStudents > 0 &&
                    attendancePercent < LowAttendanceThreshold;

                string urgencyCssClass = ResolveLiveLectureUrgencyCss(
                    now,
                    lecture,
                    todayAttendance,
                    attendancePercent);

                result.Add(new LiveAttendanceRowViewModel
                {
                    LectureId = lecture.LectureId,
                    LectureTitle = lecture.Title,
                    BatchName = lecture.BatchName,
                    InstructorName = lecture.InstructorName,
                    ConnectedStudents = connectedStudents,
                    TotalStudents = totalStudents,
                    AttendancePercent = attendancePercent,
                    StartAt = lecture.StartAt,
                    StatusText = statusText,
                    CanWarnInstructor = canWarnInstructor,
                    ActionText = canWarnInstructor ? "تنبيه عاجل" : "متابعة فقط",
                    UrgencyCssClass = urgencyCssClass
                });
            }

            return result;
        }
        private List<BatchPerformanceMatrixRowViewModel> BuildBatchPerformanceMatrix(
            List<BatchBasicProjection> batches,
            List<StudentBatchProjection> studentBatches,
            List<AttendanceProjection> todayAttendance,
            List<HomeworkSetStudentProjection> homeworkStudentRows,
            List<ExamStatusProjection> examStatusRows)
        {
            List<BatchPerformanceMatrixRowViewModel> result = new List<BatchPerformanceMatrixRowViewModel>();

            foreach (BatchBasicProjection batch in batches.OrderBy(item => item.BatchName))
            {
                List<StudentBatchProjection> batchStudents = studentBatches
                    .Where(item => item.BatchId == batch.BatchId && item.IsActive)
                    .ToList();

                int studentsCount = batchStudents.Select(item => item.StudentId).Distinct().Count();

                int presentToday = (
                    from attendance in todayAttendance
                    join studentBatch in batchStudents
                        on attendance.StudentId equals studentBatch.StudentId
                    where attendance.IsPresent
                    select attendance.StudentId)
                    .Distinct()
                    .Count();

                double attendancePercent = CalculatePercent(presentToday, studentsCount);

                List<HomeworkSetStudentProjection> batchHomeworkRows = homeworkStudentRows
                    .Where(item => item.BatchId == batch.BatchId)
                    .ToList();

                int homeworkExpected = batchHomeworkRows.Count;
                int homeworkSubmitted = batchHomeworkRows.Count(item => item.IsSubmitted);
                double homeworkSubmissionPercent = CalculatePercent(homeworkSubmitted, homeworkExpected);

                List<ExamStatusProjection> batchExamRows = examStatusRows
                    .Where(item => item.BatchId == batch.BatchId && item.Score.HasValue)
                    .ToList();

                double examAverage = 0;

                if (batchExamRows.Count > 0)
                {
                    examAverage = Math.Round(batchExamRows.Average(item => item.Score.Value), 2);
                }

                double healthScore = Math.Round(
                    (attendancePercent * 0.30) +
                    (homeworkSubmissionPercent * 0.30) +
                    (examAverage * 0.40),
                    2);

                string healthText = "مستقرة";
                string healthCss = "health-good";
                string decisionHint = "لا يلزم تدخل الآن";

                if (healthScore < 60)
                {
                    healthText = "تحتاج تدخل";
                    healthCss = "health-danger";
                    decisionHint = "افتح تقرير الدفعة وأرسل تنبيه متابعة";
                }
                else if (healthScore < 75)
                {
                    healthText = "تحتاج متابعة";
                    healthCss = "health-warning";
                    decisionHint = "تابع الواجبات والحضور خلال اليوم";
                }

                result.Add(new BatchPerformanceMatrixRowViewModel
                {
                    BatchId   = batch.BatchId,
                    BatchName = batch.BatchName,
                    IsPartner = batch.IsPartner,
                    StudentsCount = studentsCount,
                    AttendancePercentToday = attendancePercent,
                    HomeworkSubmissionPercent = homeworkSubmissionPercent,
                    ExamAverageScore = examAverage,
                    HealthScore = healthScore,
                    HealthStatusText = healthText,
                    HealthCssClass = healthCss,
                    DecisionHint = decisionHint
                });
            }

            return result;
        }

        private List<InstructorPerformanceRowViewModel> BuildInstructorPerformance(
            List<LectureProjection> weekLectures,
            List<HomeworkSetProjection> weekHomeworkSets,
            List<ExamAssignmentProjection> weekExamAssignments,
            List<AttendanceProjection> todayAttendance,
            List<StudentBatchProjection> studentBatches)
        {
            List<InstructorPerformanceRowViewModel> result = new List<InstructorPerformanceRowViewModel>();

            List<InstructorBasicProjection> instructors = weekLectures
                .Select(item => new InstructorBasicProjection
                {
                    InstructorId = item.InstructorId,
                    InstructorName = item.InstructorName
                })
                .GroupBy(item => item.InstructorId)
                .Select(group => group.First())
                .OrderBy(item => item.InstructorName)
                .ToList();

            foreach (InstructorBasicProjection instructor in instructors)
            {
                List<LectureProjection> instructorLectures = weekLectures
                    .Where(item => item.InstructorId == instructor.InstructorId)
                    .ToList();

                int lectureCount = instructorLectures.Count;

                int homeworkCount = weekHomeworkSets
                    .Count(item => item.AssignedByUserId != null);

                int examCount = weekExamAssignments
                    .Count(item => item.CreatedByInstructorId.HasValue
                                   && item.CreatedByInstructorId.Value == instructor.InstructorId);

                int possibleStudents = 0;
                int participatingStudents = 0;

                foreach (LectureProjection lecture in instructorLectures)
                {
                    int batchStudentsCount = studentBatches
                        .Count(item => item.BatchId == lecture.BatchId && item.IsActive);

                    int presentCount = todayAttendance
                        .Where(item => item.LectureId == lecture.LectureId && item.IsPresent)
                        .Select(item => item.StudentId)
                        .Distinct()
                        .Count();

                    possibleStudents += batchStudentsCount;
                    participatingStudents += presentCount;
                }

                double participationPercent = CalculatePercent(participatingStudents, possibleStudents);

                double contentActivityScore = Math.Min(100, ((homeworkCount * 12) + (examCount * 18) + (lectureCount * 10)));
                double activityScore = Math.Round((contentActivityScore * 0.55) + (participationPercent * 0.45), 2);

                string statusText = "نشاط مستقر";
                string statusCss = "status-good";
                string actionHint = "لا يلزم تدخل الآن";

                if (activityScore < 55)
                {
                    statusText = "نشاط منخفض";
                    statusCss = "status-danger";
                    actionHint = "راجع خطة المدرب لهذا الأسبوع";
                }
                else if (activityScore < 75)
                {
                    statusText = "يحتاج متابعة";
                    statusCss = "status-warning";
                    actionHint = "تابع إرسال الواجبات والاختبارات";
                }

                result.Add(new InstructorPerformanceRowViewModel
                {
                    InstructorId = instructor.InstructorId,
                    InstructorName = instructor.InstructorName,
                    LecturesThisWeek = lectureCount,
                    HomeworksThisWeek = homeworkCount,
                    ExamsThisWeek = examCount,
                    StudentParticipationPercent = participationPercent,
                    ActivityScore = activityScore,
                    StatusText = statusText,
                    StatusCssClass = statusCss,
                    ActionHint = actionHint
                });
            }

            return result;
        }

        private List<PredictiveCalendarDayViewModel> BuildPredictiveCalendar(
            DateTime todayStart,
            List<PredictiveLectureProjection> lectures,
            List<PredictiveExamProjection> exams,
            List<PredictiveHomeworkProjection> homeworks)
        {
            List<PredictiveCalendarDayViewModel> result = new List<PredictiveCalendarDayViewModel>();

            CultureInfo culture = new CultureInfo("ar-SA");

            for (int dayIndex = 0; dayIndex < 7; dayIndex++)
            {
                DateTime currentDay = todayStart.AddDays(dayIndex);
                DateTime nextDay = currentDay.AddDays(1);

                int lecturesCount = lectures.Count(item => item.StartAt >= currentDay && item.StartAt < nextDay);
                int examsCount = exams.Count(item => item.ScheduledDate >= currentDay && item.ScheduledDate < nextDay);
                int homeworksClosingCount = homeworks.Count(item => item.EndAt >= currentDay && item.EndAt < nextDay);

                bool hasWarning = examsCount > 2 || lecturesCount > 6 || homeworksClosingCount > 4;

                string warningText = string.Empty;

                if (examsCount > 2)
                {
                    warningText = "كثافة اختبارات مرتفعة";
                }
                else if (lecturesCount > 6)
                {
                    warningText = "كثافة محاضرات مرتفعة";
                }
                else if (homeworksClosingCount > 4)
                {
                    warningText = "واجبات كثيرة تغلق في نفس اليوم";
                }

                result.Add(new PredictiveCalendarDayViewModel
                {
                    Date = currentDay,
                    DayName = culture.DateTimeFormat.GetDayName(currentDay.DayOfWeek),
                    LecturesCount = lecturesCount,
                    ExamsCount = examsCount,
                    HomeworksClosingCount = homeworksClosingCount,
                    HasDensityWarning = hasWarning,
                    WarningText = warningText
                });
            }

            return result;
        }

        private List<SmartAlertViewModel> BuildSmartAlerts(AdminOperationsDashboardViewModel model)
        {
            List<SmartAlertViewModel> alerts = new List<SmartAlertViewModel>();

            alerts.AddRange(BuildPulseAlerts(model.Kpis, model.LiveAttendance));

            foreach (BatchPerformanceMatrixRowViewModel batch in model.BatchPerformanceMatrix)
            {
                if (batch.HealthScore < 60)
                {
                    alerts.Add(new SmartAlertViewModel
                    {
                        Level = "danger",
                        Title = "دفعة تحتاج تدخل",
                        Message = "مؤشر صحة الدفعة " + batch.BatchName + " منخفض: " + batch.HealthScore.ToString("0.##") + "%.",
                        SuggestedActionText = "فتح تقرير الدفعة",
                        TargetUrl = "/Admin/Batches/Details/" + batch.BatchId,
                        ContextLabel = batch.StudentsCount + " طالب",
                        IsUrgent = true
                    });
                }
                else if (batch.HomeworkSubmissionPercent < 60 && batch.StudentsCount > 0)
                {
                    alerts.Add(new SmartAlertViewModel
                    {
                        Level = "warning",
                        Title = "تسليم واجبات منخفض",
                        Message = "الدفعة " + batch.BatchName + " نسبة تسليم الواجبات لديها " + batch.HomeworkSubmissionPercent.ToString("0.##") + "%.",
                        SuggestedActionText = "مراجعة الواجبات",
                        TargetUrl = "/Admin/HomeworkManagement",
                        ContextLabel = batch.StudentsCount + " طالب",
                        IsUrgent = false
                    });
                }
            }

            foreach (InstructorPerformanceRowViewModel instructor in model.InstructorPerformance)
            {
                if (instructor.ActivityScore < 55)
                {
                    alerts.Add(new SmartAlertViewModel
                    {
                        Level = "warning",
                        Title = "نشاط مدرب منخفض",
                        Message = "المدرب " + instructor.InstructorName + " لديه مؤشر نشاط " + instructor.ActivityScore.ToString("0.##") + "% هذا الأسبوع.",
                        SuggestedActionText = "فتح ملف المدرب",
                        TargetUrl = "/Admin/Instructors/Details/" + instructor.InstructorId,
                        ContextLabel = instructor.LecturesThisWeek + " محاضرات",
                        IsUrgent = false
                    });
                }
            }

            foreach (PredictiveCalendarDayViewModel day in model.PredictiveCalendar)
            {
                if (day.HasDensityWarning)
                {
                    alerts.Add(new SmartAlertViewModel
                    {
                        Level = "warning",
                        Title = "ازدحام قادم",
                        Message = day.DayName + " " + day.Date.ToString("yyyy/MM/dd") + ": " + day.WarningText + ".",
                        SuggestedActionText = "مراجعة الجدولة",
                        TargetUrl = "/Admin/ExamAssignments",
                        ContextLabel = "خلال 7 أيام",
                        IsUrgent = false
                    });
                }
            }

            return alerts
                .OrderByDescending(item => item.IsUrgent)
                .ThenBy(item => item.Level == "danger" ? 0 : item.Level == "warning" ? 1 : 2)
                .Take(12)
                .ToList();
        }

        private List<SmartAlertViewModel> BuildPulseAlerts(
            AdminDashboardKpiViewModel kpis,
            List<LiveAttendanceRowViewModel> liveAttendance)
        {
            List<SmartAlertViewModel> alerts = new List<SmartAlertViewModel>();

            if (kpis.LowAttendanceLecturesToday > 0)
            {
                alerts.Add(new SmartAlertViewModel
                {
                    Level = "danger",
                    Title = "محاضرات بحضور منخفض",
                    Message = "يوجد " + kpis.LowAttendanceLecturesToday + " محاضرة اليوم أقل من " + LowAttendanceThreshold + "% حضور.",
                    SuggestedActionText = "مراجعة الحضور",
                    TargetUrl = "/Admin/Attendance",
                    ContextLabel = "طارئ الآن",
                    IsUrgent = true
                });
            }

            if (kpis.HomeworksClosingWithin24Hours > 0)
            {
                alerts.Add(new SmartAlertViewModel
                {
                    Level = "warning",
                    Title = "واجبات تغلق قريبًا",
                    Message = "يوجد " + kpis.HomeworksClosingWithin24Hours + " واجب يقترب من موعد الإغلاق خلال 24 ساعة.",
                    SuggestedActionText = "مراجعة الواجبات",
                    TargetUrl = "/Admin/HomeworkManagement",
                    ContextLabel = "خلال 24 ساعة",
                    IsUrgent = false
                });
            }

            foreach (LiveAttendanceRowViewModel row in liveAttendance)
            {
                if (row.CanWarnInstructor)
                {
                    alerts.Add(new SmartAlertViewModel
                    {
                        Level = row.AttendancePercent < CriticalAttendanceThreshold ? "danger" : "warning",
                        Title = "حضور منخفض في محاضرة جارية",
                        Message = "دفعة " + row.BatchName + " مع " + row.InstructorName + " لديها " + row.ConnectedStudents + " من " + row.TotalStudents + " طالب فقط.",
                        SuggestedActionText = "تنبيه عاجل",
                        TargetUrl = "#",
                        ContextLabel = row.AttendancePercent.ToString("0.##") + "%",
                        IsUrgent = row.AttendancePercent < CriticalAttendanceThreshold
                    });
                }
            }

            return alerts
                .OrderByDescending(item => item.IsUrgent)
                .Take(8)
                .ToList();
        }

        private int CountLowAttendanceLectures(
        DateTime now,
        List<LectureProjection> todayLectures,
        List<AttendanceProjection> todayAttendance,
        List<StudentBatchProjection> studentBatches)
        {
            int count = 0;

            foreach (LectureProjection lecture in todayLectures)
            {
                bool isRunningNow = IsLectureRunningNow(now, lecture, todayAttendance);

                if (!isRunningNow)
                {
                    continue;
                }

                int totalStudents = studentBatches.Count(item =>
                    item.BatchId == lecture.BatchId &&
                    item.IsActive);

                int presentStudents = todayAttendance
                    .Where(item => item.LectureId == lecture.LectureId && item.IsPresent)
                    .Select(item => item.StudentId)
                    .Distinct()
                    .Count();

                double percent = CalculatePercent(presentStudents, totalStudents);

                if (totalStudents > 0 && percent < LowAttendanceThreshold)
                {
                    count++;
                }
            }

            return count;
        }
        private string ResolveLectureStatusCode(DateTime now, LectureProjection lecture, double attendancePercent)
        {
            if (lecture.StartAt > now)
            {
                return "scheduled";
            }

            if (lecture.StartAt <= now && lecture.EndAt >= now)
            {
                if (attendancePercent < CriticalAttendanceThreshold)
                {
                    return "critical";
                }

                if (attendancePercent < LowAttendanceThreshold)
                {
                    return "running-low";
                }

                return "running";
            }

            if (lecture.EndAt < now && attendancePercent < LowAttendanceThreshold)
            {
                return "completed-low";
            }

            return "completed";
        }

        private string ResolveLectureStatusText(string statusCode)
        {
            if (statusCode == "scheduled")
            {
                return "مقررة";
            }

            if (statusCode == "running")
            {
                return "جارية الآن";
            }

            if (statusCode == "running-low")
            {
                return "جارية بحضور منخفض";
            }

            if (statusCode == "critical")
            {
                return "طارئة الآن";
            }

            if (statusCode == "completed-low")
            {
                return "انتهت بحضور منخفض";
            }

            return "اكتملت";
        }

        private string BuildTrendText(double current, double previous, string unit)
        {
            double difference = Math.Round(current - previous, 2);

            if (previous == 0 && current == 0)
            {
                return "لا تغيير عن الأمس";
            }

            if (difference > 0)
            {
                return "+" + difference.ToString("0.##") + " " + unit + " عن الأمس";
            }

            if (difference < 0)
            {
                return difference.ToString("0.##") + " " + unit + " عن الأمس";
            }

            return "لا تغيير عن الأمس";
        }

        private double CalculatePercent(int value, int total)
        {
            if (total <= 0)
            {
                return 0;
            }

            return Math.Round((value / (double)total) * 100, 2);
        }

        private class BatchBasicProjection
        {
            public int BatchId { get; set; }

            public string BatchName { get; set; } = string.Empty;

            public bool IsPartner { get; set; }
        }

        private class StudentBatchProjection
        {
            public int StudentId { get; set; }

            public int BatchId { get; set; }

            public bool IsActive { get; set; }

            public DateTime? LastLoginAt { get; set; }
        }

        private class LectureProjection
        {
            public int LectureId { get; set; }

            public string Title { get; set; } = string.Empty;

            public int BatchId { get; set; }

            public string BatchName { get; set; } = string.Empty;

            public int InstructorId { get; set; }

            public string InstructorName { get; set; } = string.Empty;

            public DateTime StartAt { get; set; }

            public DateTime EndAt { get; set; }
        }

        private class AttendanceProjection
        {
            public int LectureId { get; set; }

            public int StudentId { get; set; }

            public bool IsPresent { get; set; }

            public DateTime RecordedAt { get; set; }
        }

        private class HomeworkSetProjection
        {
            public int HomeworkSetId { get; set; }

            public int BatchId { get; set; }

            public string BatchName { get; set; } = string.Empty;

            public DateTime CreatedAt { get; set; }

            public DateTime? EndAt { get; set; }

            public bool IsSent { get; set; }

            public string? AssignedByUserId { get; set; }
        }

        private class HomeworkSetStudentProjection
        {
            public int HomeworkSetId { get; set; }

            public int BatchId { get; set; }

            public int StudentId { get; set; }

            public bool IsSubmitted { get; set; }

            public DateTime? SubmittedAt { get; set; }

            public double? Score { get; set; }
        }

        private class ExamAssignmentProjection
        {
            public int ExamAssignmentId { get; set; }

            public int BatchId { get; set; }

            public string BatchName { get; set; } = string.Empty;

            public DateTime CreatedAt { get; set; }

            public DateTime? ScheduledDate { get; set; }

            public int DurationMinutes { get; set; }

            public bool IsSentToStudents { get; set; }

            public int? CreatedByInstructorId { get; set; }
        }

        private class ExamStatusProjection
        {
            public int ExamAssignmentId { get; set; }

            public int BatchId { get; set; }

            public int StudentId { get; set; }

            public bool IsSubmitted { get; set; }

            public int? Score { get; set; }

            public DateTime AssignedAt { get; set; }

            public DateTime? SubmittedAt { get; set; }
        }

        private class InstructorBasicProjection
        {
            public int InstructorId { get; set; }

            public string InstructorName { get; set; } = string.Empty;
        }

        private class PredictiveLectureProjection
        {
            public int LectureId { get; set; }

            public DateTime Date { get; set; }

            public DateTime StartAt { get; set; }

            public DateTime EndAt { get; set; }

            public int BatchId { get; set; }
        }

        private class PredictiveExamProjection
        {
            public int ExamAssignmentId { get; set; }

            public int BatchId { get; set; }

            public DateTime ScheduledDate { get; set; }

            public int DurationMinutes { get; set; }

            public bool IsSentToStudents { get; set; }
        }

        private class PredictiveHomeworkProjection
        {
            public int HomeworkSetId { get; set; }

            public int BatchId { get; set; }

            public DateTime EndAt { get; set; }

            public bool IsSent { get; set; }
        }


        private bool IsLectureRunningNow(
    DateTime now,
    LectureProjection lecture,
    List<AttendanceProjection> todayAttendance)
        {
            bool insideScheduledWindow =
                lecture.StartAt <= now &&
                lecture.EndAt >= now;

            if (insideScheduledWindow)
            {
                return true;
            }

            DateTime liveAttendanceStart = now.AddMinutes(-LiveLectureAttendanceWindowMinutes);

            bool hasRecentAttendance = todayAttendance.Any(item =>
                item.LectureId == lecture.LectureId &&
                item.RecordedAt >= liveAttendanceStart &&
                item.RecordedAt <= now);

            return hasRecentAttendance;
        }

        private string ResolveLiveLectureStatusText(
            DateTime now,
            LectureProjection lecture,
            List<AttendanceProjection> todayAttendance,
            double attendancePercent)
        {
            bool isRunningNow = IsLectureRunningNow(now, lecture, todayAttendance);

            if (isRunningNow)
            {
                if (attendancePercent < CriticalAttendanceThreshold)
                {
                    return "جارية الآن - حضور حرج";
                }

                if (attendancePercent < LowAttendanceThreshold)
                {
                    return "جارية الآن - حضور منخفض";
                }

                return "جارية الآن";
            }

            if (lecture.StartAt > now)
            {
                return "مقررة";
            }

            return "انتهت";
        }

        private string ResolveLiveLectureUrgencyCss(
            DateTime now,
            LectureProjection lecture,
            List<AttendanceProjection> todayAttendance,
            double attendancePercent)
        {
            bool isRunningNow = IsLectureRunningNow(now, lecture, todayAttendance);

            if (!isRunningNow)
            {
                return "normal";
            }

            if (attendancePercent < CriticalAttendanceThreshold)
            {
                return "critical";
            }

            if (attendancePercent < LowAttendanceThreshold)
            {
                return "warning";
            }

            return "normal";
        }



    }
}