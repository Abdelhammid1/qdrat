using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Security;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Reports;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class PerformanceExamReportsController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IPerformanceInsightService _performanceInsightService;

        public PerformanceExamReportsController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IPerformanceInsightService performanceInsightService)
        {
            _contextFactory = contextFactory;
            _performanceInsightService = performanceInsightService;
        }

        // ✅ عرض تقرير الطالب التفصيلي لاختبار مؤشر الأداء
        [HttpGet]
        [AdminPermission("PerformanceExamReports", "StudentReport")]
        public async Task<IActionResult> StudentExamReport(int studentId, int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // ============================
            // 1) تحميل بيانات الطالب + الاختبار
            // ============================
            var student = await _context.Students
                .Include(s => s.BatchEnrollments)
                .ThenInclude(b => b.Batch)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            var exam = await _context.PerformanceIndicatorExams
                .Include(e => e.Curriculum)
                .Include(e => e.Batch)
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (student == null || exam == null)
                return NotFound("⚠️ لم يتم العثور على الطالب أو بيانات الاختبار.");

            var studentBatchIds = student.BatchEnrollments
                .Select(e => e.BatchId)
                .Distinct()
                .ToList();

            var examBatchIds = await _context.PerformanceIndicatorExamToBatch
                .AsNoTracking()
                .Where(x => x.PerformanceIndicatorExamId == examId)
                .Select(x => x.BatchId)
                .ToListAsync();

            int? reportBatchId = exam.BatchId.HasValue && studentBatchIds.Any(id => id == exam.BatchId.Value)
                ? exam.BatchId.Value
                : examBatchIds.FirstOrDefault(batchId => studentBatchIds.Any(id => id == batchId));

            reportBatchId ??= exam.BatchId;

            var reportBatchName = reportBatchId.HasValue
                ? await _context.Batches
                    .AsNoTracking()
                    .Where(b => b.Id == reportBatchId.Value)
                    .Select(b => b.Name)
                    .FirstOrDefaultAsync()
                : exam.Batch?.Name;

            // 🟢 الطالب قد يكون مسجّلاً في أكثر من دفعة (تحويل بين الدفعات مثلاً)،
            // لذلك نجمع كل الدفعات التي حضر فيها الطالب فعلياً ضمن نطاق دفعات هذا الاختبار،
            // بدلاً من الاقتصار على دفعة واحدة فقط عند جلب المحاضرات/الحضور/الواجبات.
            var examRelevantBatchIds = examBatchIds.Any()
                ? examBatchIds
                : (exam.BatchId.HasValue ? new List<int> { exam.BatchId.Value } : new List<int>());

            var reportBatchIds = studentBatchIds
                .Where(id => examRelevantBatchIds.Contains(id))
                .ToList();

            if (!reportBatchIds.Any() && reportBatchId.HasValue)
                reportBatchIds.Add(reportBatchId.Value);

            // ============================
            // 2) تحميل جميع أسئلة الاختبار
            // ============================
            var examQuestions = await _context.PerformanceIndicatorExamQuestions
                .Where(eq => eq.PerformanceIndicatorExamId == examId)
                .Select(eq => new { eq.QuestionId, eq.SectionId })
                .ToListAsync();

            int totalQuestions = examQuestions.Count;

            // ============================
            // 3) تحميل محاولات الطالب
            // ============================
            var attempts = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                .ThenInclude(q => q.Lesson)
                .ThenInclude(l => l.Section)
                .Where(a => a.StudentId == studentId && a.PerformanceIndicatorExamId == examId)
                .ToListAsync();

            // ============================
            // 4) الحساب العام (صحيح / خطأ / متخطى)
            // ============================
            int correct = attempts.Count(a => a.IsCorrect);

            int wrong = attempts.Count(a =>
                !a.IsCorrect &&
                !string.IsNullOrWhiteSpace(a.SelectedAnswer) &&
                a.SelectedAnswer != "—"
            );

            int answeredReal = attempts.Count(a =>
                !string.IsNullOrWhiteSpace(a.SelectedAnswer) &&
                a.SelectedAnswer != "—"
            );

            int skipped = totalQuestions - answeredReal;

            double percent = totalQuestions > 0
                ? Math.Round(correct * 100.0 / totalQuestions, 1)
                : 0;

            // ============================
            //  🕒 الوقت الحقيقي المستغرق
            // ============================

            string elapsedTimeFormatted = "غير محدد";
            double totalSeconds = 0;

            // محاولة أولى وآخيرة
            var firstAttempt = attempts
                .OrderBy(a => a.AttemptedAt)
                .FirstOrDefault();

            var lastAttempt = attempts
                .OrderByDescending(a => a.AttemptedAt)
                .FirstOrDefault();

            if (firstAttempt != null && lastAttempt != null)
            {
                var duration = lastAttempt.AttemptedAt - firstAttempt.AttemptedAt;

                totalSeconds = duration.TotalSeconds;

                if (totalSeconds > 0)
                {
                    int minutes = (int)duration.TotalMinutes;
                    int seconds = (int)(duration.TotalSeconds % 60);
                    elapsedTimeFormatted = $"{minutes} دقيقة و {seconds} ثانية";
                }
            }
            else
            {
                // 🟢 خطة احتياطية: استخدام StartedAt و CompletedAt
                var studentExam = await _context.PerformanceIndicatorExamStudents
                    .FirstOrDefaultAsync(s => s.PerformanceIndicatorExamId == examId &&
                                              s.StudentId == studentId);

                if (studentExam?.StartedAt != null && studentExam.CompletedAt != null)
                {
                    var duration = studentExam.CompletedAt.Value - studentExam.StartedAt.Value;

                    totalSeconds = duration.TotalSeconds;

                    int minutes = (int)duration.TotalMinutes;
                    int seconds = (int)(duration.TotalSeconds % 60);

                    elapsedTimeFormatted = $"{minutes} دقيقة و {seconds} ثانية";
                }
            }

            // ============================
            // 5) تحميل عناوين المحاور
            // ============================
            var sectionsDictionary = await _context.Sections
                .ToDictionaryAsync(s => s.Id, s => s.Title);

            // ============================
            // 6) تحليل كل محور: صحيحة / خاطئة / متخطاة / نسبة
            // ============================
            var sections = new List<SectionReportItem>();

            var grouped = examQuestions.GroupBy(eq => eq.SectionId).ToList();

            foreach (var sg in grouped)
            {
                int realSectionId = sg.Key ?? 0;

                string title = sectionsDictionary.ContainsKey(realSectionId)
                    ? sectionsDictionary[realSectionId]
                    : "محور غير معروف";

                var questionIds = sg.Select(x => x.QuestionId).ToList();

                // محاولات الطالب لهذا المحور
                var attemptsForSection = attempts
                    .Where(a => questionIds.Contains(a.QuestionId))
                    .ToList();

                int secCorrect = attemptsForSection.Count(a => a.IsCorrect);

                int secWrong = attemptsForSection.Count(a =>
                    !a.IsCorrect &&
                    !string.IsNullOrWhiteSpace(a.SelectedAnswer) &&
                    a.SelectedAnswer != "—"
                );

                int secAnsweredReal = attemptsForSection.Count(a =>
                    !string.IsNullOrWhiteSpace(a.SelectedAnswer) &&
                    a.SelectedAnswer != "—"
                );

                int secSkipped = questionIds.Count - secAnsweredReal;

                // النسبة الجديدة = صحيحة ÷ (صحيحة + خاطئة + متخطاة)
                double secPercent = (secCorrect + secWrong + secSkipped) > 0
                    ? Math.Round(secCorrect * 100.0 / (secCorrect + secWrong + secSkipped), 1)
                    : 0;

                sections.Add(new SectionReportItem
                {
                    SectionTitle = title,
                    Total = questionIds.Count,
                    Correct = secCorrect,
                    Wrong = secWrong,
                    Skipped = secSkipped,
                    Guidance = _performanceInsightService.GetSectionGuidance(secPercent)
                });
            }

            // ============================
            // 7) اسم المدرّس
            // ============================
            var instructorName = await _context.InstructorCurriculumBatches
                .Where(icb => icb.BatchId == exam.BatchId && icb.CurriculumId == exam.CurriculumId)
                .Select(icb => icb.Instructor.FullName)
                .FirstOrDefaultAsync() ?? "غير محدد";

            // ============================
            // 8) تحليل الذكاء الاصطناعي
            // ============================
            var insight = await _performanceInsightService.AnalyzeStudentPerformanceAsync(studentId, examId);




            // ============================
            //  🧮 حساب نسبة الوقت المستهلك
            // ============================
            int examDurationSeconds = exam.DurationMinutes * 60;

            double timeUsagePercent = 0;

            if (examDurationSeconds > 0 && totalSeconds > 0)
            {
                timeUsagePercent = Math.Round((totalSeconds / examDurationSeconds) * 100.0, 1);

                // لا تتجاوز 100% في أي حال
                if (timeUsagePercent > 100)
                    timeUsagePercent = 100;
            }



            // ============================
            // 9) حضور المحاضرات والواجبات المرتبطة بمحاضرات الدفعة
            // ============================
            var lectureAttendances = new List<LectureAttendanceItem>();
            var homeworkItems = new List<HomeworkItem>();
            double homeworkAverage = 0;

            if (reportBatchIds.Any())
            {
                var lectureRows = await _context.Lecture
                    .AsNoTracking()
                    .Where(l =>
                        reportBatchIds.Contains(l.BatchId) &&
                        l.Section.CurriculumId == exam.CurriculumId)
                    .OrderBy(l => l.Date)
                    .Select(l => new
                    {
                        l.Id,
                        l.Title,
                        l.Date,
                        l.ScheduledTime
                    })
                    .ToListAsync();

                var attendanceRows = await (
                    from attendance in _context.AttendanceRecords.AsNoTracking()
                    join lecture in _context.Lecture.AsNoTracking()
                        on attendance.LectureId equals lecture.Id
                    where attendance.StudentId == studentId
                          && reportBatchIds.Contains(lecture.BatchId)
                          && lecture.Section.CurriculumId == exam.CurriculumId
                    select new
                    {
                        attendance.LectureId,
                        attendance.IsPresent,
                        attendance.IsLateArrival,
                        attendance.HasEarlyLeavePermission,
                        attendance.ActualArrivalTime
                    })
                    .ToListAsync();

                lectureAttendances = lectureRows
                    .Select((lecture, index) =>
                    {
                        var attendance = attendanceRows.FirstOrDefault(a => a.LectureId == lecture.Id);
                        var status = attendance == null || !attendance.IsPresent
                            ? "غائب"
                            : attendance.IsLateArrival
                                ? "متأخر"
                                : "حاضر";

                        var lateMinutes = 0;
                        if (attendance?.ActualArrivalTime != null && lecture.ScheduledTime != null &&
                            attendance.ActualArrivalTime.Value > lecture.ScheduledTime.Value)
                        {
                            lateMinutes = (int)Math.Round((attendance.ActualArrivalTime.Value - lecture.ScheduledTime.Value).TotalMinutes);
                        }

                        return new LectureAttendanceItem
                        {
                            LectureNumber = index + 1,
                            LectureTitle = lecture.Title,
                            LectureDate = lecture.Date,
                            AttendanceStatus = status,
                            LateMinutes = Math.Max(0, lateMinutes),
                            PermissionsCount = attendance?.HasEarlyLeavePermission == true ? 1 : 0
                        };
                    })
                    .ToList();

                // 🟢 CurriculumId على HomeworkSet كثيراً ما يكون فارغاً (المسار الفعلي لإسناد الواجبات
                // لا يعبّئه) — لذلك نحدد منهج الواجب إما من الحقل مباشرة، أو من محاضرة الواجب المرتبطة به،
                // بدل الاعتماد الحصري على حقل قد يكون غير معبأ في البيانات الحقيقية.
                var batchLectureCurriculumMap = await _context.Lecture
                    .AsNoTracking()
                    .Where(l => reportBatchIds.Contains(l.BatchId))
                    .Select(l => new { l.Id, CurriculumId = l.Section.CurriculumId })
                    .ToDictionaryAsync(x => x.Id, x => x.CurriculumId);

                bool MatchesExamCurriculum(int? hsCurriculumId, int? hsLectureId) =>
                    hsCurriculumId == exam.CurriculumId ||
                    (hsLectureId.HasValue &&
                     batchLectureCurriculumMap.TryGetValue(hsLectureId.Value, out var lectureCurriculumId) &&
                     lectureCurriculumId == exam.CurriculumId);

                var submittedHomeworkRowsRaw = await (
                    from hss in _context.HomeworkSetStudents.AsNoTracking()
                    join hs in _context.HomeworkSets.AsNoTracking()
                        on hss.HomeworkSetId equals hs.Id
                    where hss.StudentId == studentId
                          && reportBatchIds.Contains(hs.BatchId)
                    select new
                    {
                        hs.Id,
                        hs.Title,
                        hs.CompletionTitle,
                        hs.CurriculumId,
                        hs.LectureId,
                        hss.IsSubmitted,
                        hss.Score
                    })
                    .Distinct()
                    .ToListAsync();

                var submittedHomeworkRows = submittedHomeworkRowsRaw
                    .Where(hs => MatchesExamCurriculum(hs.CurriculumId, hs.LectureId))
                    .ToList();

                var assignedHomeworkRowsRaw = await (
                    from homework in _context.Homeworks.AsNoTracking()
                    join hs in _context.HomeworkSets.AsNoTracking()
                        on homework.HomeworkSetId equals hs.Id
                    where homework.StudentId == studentId
                          && reportBatchIds.Contains(hs.BatchId)
                    select new
                    {
                        hs.Id,
                        hs.Title,
                        hs.CompletionTitle,
                        hs.CurriculumId,
                        hs.LectureId,
                        homework.QuestionId
                    })
                    .ToListAsync();

                var assignedHomeworkRows = assignedHomeworkRowsRaw
                    .Where(x => MatchesExamCurriculum(x.CurriculumId, x.LectureId))
                    .GroupBy(x => new { x.Id, x.Title, x.CompletionTitle })
                    .Select(g => new
                    {
                        g.Key.Id,
                        g.Key.Title,
                        g.Key.CompletionTitle,
                        TotalQuestions = g.Select(x => x.QuestionId).Distinct().Count()
                    })
                    .ToList();

                var attemptScoresRaw = await (
                    from attempt in _context.QuestionAttemptNew.AsNoTracking()
                    join homework in _context.Homeworks.AsNoTracking()
                        on new { attempt.StudentId, attempt.QuestionId, attempt.HomeworkSetId }
                        equals new { homework.StudentId, homework.QuestionId, HomeworkSetId = (int?)homework.HomeworkSetId }
                    join hs in _context.HomeworkSets.AsNoTracking()
                        on homework.HomeworkSetId equals hs.Id
                    where attempt.StudentId == studentId
                          && reportBatchIds.Contains(hs.BatchId)
                    select new
                    {
                        hs.Id,
                        hs.CurriculumId,
                        hs.LectureId,
                        attempt.QuestionId,
                        attempt.IsCorrect
                    })
                    .ToListAsync();

                var attemptScores = attemptScoresRaw
                    .Where(x => MatchesExamCurriculum(x.CurriculumId, x.LectureId))
                    .GroupBy(x => x.Id)
                    .Select(g => new
                    {
                        HomeworkSetId = g.Key,
                        TotalAttempts = g.Select(x => x.QuestionId).Distinct().Count(),
                        CorrectAttempts = g.Where(x => x.IsCorrect).Select(x => x.QuestionId).Distinct().Count()
                    })
                    .ToList();

                homeworkItems = assignedHomeworkRows
                    .Select((homework, index) =>
                    {
                        var submitted = submittedHomeworkRows.FirstOrDefault(x => x.Id == homework.Id);
                        var attemptScore = attemptScores.FirstOrDefault(x => x.HomeworkSetId == homework.Id);
                        var score = submitted?.Score;

                        if (!score.HasValue && attemptScore != null && attemptScore.TotalAttempts > 0)
                        {
                            score = Math.Round(attemptScore.CorrectAttempts * 100.0 / attemptScore.TotalAttempts, 1);
                        }

                        return new HomeworkItem
                        {
                            Number = index + 1,
                            Title = !string.IsNullOrWhiteSpace(homework.Title) ? homework.Title : homework.CompletionTitle,
                            IsSubmitted = submitted?.IsSubmitted == true || score.HasValue,
                            Score = score,
                            MaxScore = 100
                        };
                    })
                    .ToList();

                homeworkAverage = homeworkItems.Any(x => x.IsSubmitted && x.Score.HasValue)
                    ? Math.Round(homeworkItems.Where(x => x.IsSubmitted && x.Score.HasValue).Average(x => x.Score!.Value), 1)
                    : 0;
            }

            // ============================
            // 9) بناء الـ ViewModel النهائي
            // ============================
            var vm = new StudentExamReportViewModel
            {
                StudentId = student.StudentID,
                StudentName = student.FullName,
                BatchName = reportBatchName ?? "غير محددة",
                CurriculumTitle = exam.Curriculum?.Title ?? "—",
                InstructorName = instructorName,
                ExamTitle = exam.Title,
                ExamDate = exam.CreatedAt,
                TimeUsagePercent = timeUsagePercent,

                TotalQuestions = totalQuestions,
                CorrectAnswers = correct,
                WrongAnswers = wrong,
                Skipped = skipped,
                OverallPercent = percent,
                ElapsedTimeFormatted = TimeSpan.FromSeconds(totalSeconds).ToString(@"hh\:mm\:ss"),
                CheatingFlagMessage = insight?.CheatingFlagMessage ?? "",

                SpeedAccuracyFeedback = insight?.SpeedAccuracyFeedback ?? "—",
                MotivationalMessage = insight?.MotivationalMessage ?? "—",
                Sections = sections,
                LectureAttendances = lectureAttendances,
                Homeworks = homeworkItems,
                HomeworkAverage = homeworkAverage
            };

            return View("ResultReport", vm);
        }


        [HttpGet]
        [AdminPermission("PerformanceExamReports", "BatchReport")]
        public async Task<IActionResult> BatchReport(int batchId, int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🟦 تحميل الدفعة وجميع الطلاب المرتبطين بها عبر جدول الربط
            var batch = await _context.Batches
                .Include(b => b.StudentBatchEnrollments)
                    .ThenInclude(sbe => sbe.Student)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                return NotFound("❌ لم يتم العثور على الدفعة.");

            // ✅ جلب الطلاب الفعليين داخل الدفعة
            var students = batch.StudentBatchEnrollments
                .Select(e => e.Student)
                .Where(s => s != null)
                .ToList();

            int totalStudents = students.Count;

            // 🧩 جلب نتائج الطلاب في اختبار مؤشرات الأداء
            var results = await _context.StudentIndicatorResults
                .Include(r => r.Section)
                .Where(r => r.PerformanceIndicatorExamId == examId)
                .ToListAsync();

            // 🟢 تصفية النتائج لتكون فقط لطلاب هذه الدفعة
            results = results.Where(r => students.Any(s => s.StudentID == r.StudentId)).ToList();

            if (!results.Any())
                return View("EmptyReport");

            // 🧮 حساب إحصاءات عامة
            var testedStudents = results.Select(r => r.StudentId).Distinct().Count();
            var notTestedStudents = totalStudents - testedStudents;

            var studentAverages = results
                .GroupBy(r => r.StudentId)
                .Select(g => new
                {
                    StudentId = g.Key,
                    AvgScore = g.Average(x => x.ScorePercent)
                })
                .ToList();

            int passCount = studentAverages.Count(s => s.AvgScore >= 60);
            int failCount = studentAverages.Count(s => s.AvgScore < 60);
            double batchAverage = Math.Round(studentAverages.Average(s => s.AvgScore), 1);

            // 🧭 تحليل الأداء حسب المحاور
            var sectionStats = results
                .GroupBy(r => r.Section.Title)
                .Select(g => new SectionSummaryVm
                {
                    SectionTitle = g.Key,
                    AvgScore = Math.Round(g.Average(x => x.ScorePercent), 1),
                    Passed = g.Count(x => x.ScorePercent >= 60),
                    Failed = g.Count(x => x.ScorePercent < 60)
                })
                .ToList();

            // 🔝 أقوى 3 محاور
            var topSections = sectionStats
                .OrderByDescending(s => s.AvgScore)
                .Take(3)
                .ToList();

            // 🔻 أضعف 3 محاور
            var weakSections = sectionStats
                .OrderBy(s => s.AvgScore)
                .Take(3)
                .ToList();

            // 🧾 حساب متوسط كل طالب
            var studentPerformance = results
                .GroupBy(r => r.StudentId)
                .Select(g => new StudentPerformanceVm
                {
                    StudentId = g.Key,
                    ExamId = examId,
                    StudentName = _context.Students
                        .Where(s => s.StudentID == g.Key)
                        .Select(s => s.FullName)
                        .FirstOrDefault(),
                    AveragePercent = Math.Round(g.Average(x => x.ScorePercent), 1)
                })
                .ToList();

            // 🧩 الطلاب الذين يحتاجون خطة علاجية (رسبوا في محور واحد أو أكثر)
            // 🧩 الطلاب الذين يحتاجون خطة علاجية (رسبوا في محور واحد أو أكثر)
            var remedialStudents = results
                .GroupBy(r => r.StudentId)
                .Select(g => new RemedialStudentVm
                {
                    StudentId = g.Key,
                    StudentName = _context.Students
                        .Where(s => s.StudentID == g.Key)
                        .Select(s => s.FullName)
                        .FirstOrDefault(),
                    FailedSections = g
                        .Where(x => x.ScorePercent < 60)
                        .Select(x => x.Section.Title)
                        .Distinct()
                        .ToList(),
                    BatchId = batchId,   // ✅ يمرر رقم الدفعة الحالية
                    ExamId = examId      // ✅ يمرر رقم الاختبار الحالي
                })
                .Where(r => r.FailedSections.Any())
                .ToList();


            // 🧠 بناء نموذج التقرير النهائي
            var vm = new BatchPerformanceReportViewModel
            {
                BatchName = batch.Name,
                ExamId = examId,
                TotalStudents = totalStudents,
                TestedStudents = testedStudents,
                NotTestedStudents = notTestedStudents,
                PassedStudents = passCount,
                FailedStudents = failCount,
                AveragePercent = batchAverage,
                SectionStats = sectionStats,
                TopSections = topSections,
                WeakSections = weakSections,
                StudentsResults = studentPerformance,
                RemedialStudents = remedialStudents
            };

            return View("BatchPerformanceReport", vm);
        }







    }
}
