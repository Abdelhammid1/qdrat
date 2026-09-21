using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Dashboard;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Instructor;
using QdratNew.ViewModels.Remedial;
using QdratNew.ViewModels.Students;

namespace QdratNew.Services.Implementations
{
    public class PerformanceDashboardService : IPerformanceDashboardService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IMemoryCache _cache;
        private readonly IStudentRankingService _studentRankingService;

        public PerformanceDashboardService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IMemoryCache cache,
            IStudentRankingService studentRankingService)
        {
            _contextFactory = contextFactory;
            _cache = cache;
            _studentRankingService = studentRankingService;
        }

        public async Task<PerformanceDashboardViewModel> GetDashboardDataAsync()
        {
            using var _context = _contextFactory.CreateDbContext();

            var now = DateTime.Now;
            var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7);

            // ============================
            // 1️⃣ جلب اختبارات الأسبوع
            // ============================
            var examsThisWeek = await _context.PerformanceIndicatorExams
                .AsNoTracking()
                .Include(e => e.Batch)
                .Include(e => e.Curriculum)
                .Where(e => e.CreatedAt >= startOfWeek && e.CreatedAt <= endOfWeek)
                .ToListAsync();

            // ============================
            // 2️⃣ جلب عدد الأسئلة لكل اختبار
            // ============================
            var questionCounts = await _context.PerformanceIndicatorExamQuestions
                .AsNoTracking()
                .GroupBy(q => q.PerformanceIndicatorExamId)
                .Select(g => new { ExamId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ExamId, x => x.Count);

            // ============================
            // 3️⃣ نتائج الطلاب (في الذاكرة)
            // ============================
            var allResults = await _context.StudentIndicatorResults
                .AsNoTracking()
                .Select(r => new { r.StudentId, r.ScorePercent })
                .ToListAsync();

            int totalStudents = 0, passed = 0, failed = 0;

            if (allResults.Any())
            {
                var studentGroups = allResults
                    .GroupBy(r => r.StudentId)
                    .Select(g => new
                    {
                        StudentId = g.Key,
                        AvgScore = g.Average(x => x.ScorePercent)
                    })
                    .ToList();

                totalStudents = studentGroups.Count;
                passed = studentGroups.Count(x => x.AvgScore >= 60);
                failed = studentGroups.Count(x => x.AvgScore < 60);
            }

            double successRate = (passed + failed) == 0
                ? 0
                : Math.Round((double)passed / (passed + failed) * 100, 1);

            // ============================
            // 4️⃣ ActiveBatches بلا Contains
            // ============================
            var resultsGrouped = await _context.StudentIndicatorResults
                .AsNoTracking()
                .GroupBy(r => r.PerformanceIndicatorExamId)
                .Select(g => new
                {
                    ExamId = g.Key,
                    StudentsCount = g.Count(),
                    FailedCount = g.Count(x => x.ScorePercent < 60)
                })
                .ToListAsync();

            var activeBatches = (
                from e in examsThisWeek
                join r in resultsGrouped on e.Id equals r.ExamId into gj
                from res in gj.DefaultIfEmpty()
                select new BatchPerformanceItem
                {
                    BatchId = e.BatchId ?? 0,
                    BatchName = e.Batch?.Name ?? "—",
                    ExamId = e.Id,
                    ExamTitle = e.Title ?? "—",
                    CurriculumTitle = e.Curriculum?.Title ?? "—",
                    CreatedAt = e.CreatedAt,
                    StudentsCount = res?.StudentsCount ?? 0,
                    FailedCount = res?.FailedCount ?? 0
                }
            )
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

            // ============================
            // 5️⃣ إضافة ترتيب الطالب بدون Contains
            // ============================
            var rankVm = await _studentRankingService.GetCurrentRankAsync(1); // ← لاحظ أنك ستضع studentId الحقيقي

            ViewModels.Exam.StudentRankViewModel studentRank = null;

            if (rankVm != null)
            {
                studentRank = new ViewModels.Exam.StudentRankViewModel
                {
                    Rank = rankVm.Rank,
                    TotalStudents = rankVm.TotalStudents,
                    MedalImageUrl = rankVm.MedalImageUrl,
                    MotivationalMessage = rankVm.MotivationalMessage,
                    LastUpdated = rankVm.LastUpdated
                };
            }

            // ============================
            // 6️⃣ بناء الـ ViewModel النهائي
            // ============================
            return new PerformanceDashboardViewModel
            {
                TotalExamsThisWeek = examsThisWeek.Count,
                TotalBatches = examsThisWeek.Select(e => e.BatchId).Distinct().Count(),
                TotalStudents = totalStudents,
                SuccessRate = successRate,
                ActiveBatches = activeBatches,
                StudentRank = studentRank == null
    ? null
    : new QdratNew.ViewModels.Students.StudentRankViewModel
    {
        Rank = studentRank.Rank,
        TotalStudents = studentRank.TotalStudents,
        MotivationalMessage = studentRank.MotivationalMessage,
        MedalImageUrl = studentRank.MedalImageUrl
    },
                ExamsThisWeek = examsThisWeek.Select(e => new ExamSummaryItem
                {
                    ExamId = e.Id,
                    BatchName = e.Batch?.Name ?? "—",
                    CurriculumTitle = e.Curriculum?.Title ?? "—",
                    CreatedAt = e.CreatedAt,
                    TotalQuestions = questionCounts.TryGetValue(e.Id, out var c) ? c : 0,
                    ReferenceCode = e.ReferenceCode
                }).ToList()
            };
        }

        // 🟢 المستوى 2 — تحليل أداء الدفعات النشطة
        public async Task<List<BatchPerformanceItem>> GetActiveBatchPerformanceAsync()
        {
            using var _context = _contextFactory.CreateDbContext();

            var batchData = await (
                from e in _context.PerformanceIndicatorExams.AsNoTracking()
                join eb in _context.PerformanceIndicatorExamToBatch.AsNoTracking()
                    on e.Id equals eb.PerformanceIndicatorExamId
                join b in _context.Batches.AsNoTracking()
                    on eb.BatchId equals b.Id
                join c in _context.Curriculums.AsNoTracking()
                    on e.CurriculumId equals c.Id
                select new BatchPerformanceItem
                {
                    BatchId = b.Id,
                    BatchName = b.Name,
                    ExamTitle = e.Title,
                    CurriculumTitle = c.Title,
                    StudentsCount = _context.StudentIndicatorResults
                        .Where(r => r.PerformanceIndicatorExamId == e.Id)
                        .Select(r => r.StudentId)
                        .Distinct()
                        .Count(),
                    FailedCount = _context.StudentIndicatorResults
                        .Count(r => r.PerformanceIndicatorExamId == e.Id && r.ScorePercent < 60),
                    CreatedAt = e.CreatedAt
                }
            )
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

            return batchData;
        }

        // 🟢 المستوى 3 — تحليل تفصيلي لدفعة معينة
        public async Task<BatchPerformanceDetailViewModel> GetBatchPerformanceDetailsAsync(int batchId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var batch = await _context.Batches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId);

            var results = await (
                from r in _context.StudentIndicatorResults.AsNoTracking()
                join s in _context.Students.AsNoTracking() on r.StudentId equals s.StudentID
                join sb in _context.StudentBatchEnrollments.AsNoTracking() on s.StudentID equals sb.StudentID
                where sb.BatchId == batchId
                select new
                {
                    r.StudentId,
                    r.SectionId,
                    r.ScorePercent,
                    r.IsPassed,
                    r.Section
                }
            ).ToListAsync();

            var bySection = results
                .GroupBy(r => r.Section.Title)
                .Select(g => new SectionPerformanceItem
                {
                    SectionTitle = g.Key,
                    AverageScore = Math.Round(g.Average(x => x.ScorePercent), 1),
                    FailedStudents = g.Count(x => x.ScorePercent < 60)
                })
                .OrderBy(x => x.AverageScore)
                .ToList();

            return new BatchPerformanceDetailViewModel
            {
                BatchId = batchId,
                BatchName = batch?.Name ?? "—",
                Sections = bySection
            };
        }


        public async Task<List<BatchExamItem>> GetExamsForBatchAsync(int batchId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var exams = await (
                from e in _context.PerformanceIndicatorExams.AsNoTracking()
                join link in _context.PerformanceIndicatorExamToBatch.AsNoTracking()
                    on e.Id equals link.PerformanceIndicatorExamId
                join c in _context.Curriculums.AsNoTracking() on e.CurriculumId equals c.Id
                where link.BatchId == batchId
                orderby e.CreatedAt descending
                select new BatchExamItem
                {
                    ExamId = e.Id,
                    BatchId = link.BatchId, // ✅ أضف هذا السطر ليُمرر batchId الصحيح
                    ExamTitle = e.Title,
                    CurriculumTitle = c.Title,
                    CreatedAt = e.CreatedAt,
                    StudentsCount = _context.StudentIndicatorResults
                        .Where(r => r.PerformanceIndicatorExamId == e.Id)
                        .Select(r => r.StudentId)
                        .Distinct()
                        .Count(),
                    FailedCount = _context.StudentIndicatorResults
                        .Count(r => r.PerformanceIndicatorExamId == e.Id && r.ScorePercent < 60)
                }
            ).ToListAsync();

            return exams;
        }

        public async Task<ExamPerformanceDetailViewModel?> GetExamPerformanceDetailsAsync(int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var exam = await _context.PerformanceIndicatorExams
                .Include(e => e.Curriculum)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null)
                return null;

            // جلب النتائج لكل محور (Section)
            var sections = await (
                from r in _context.StudentIndicatorResults.AsNoTracking()
                join s in _context.Sections.AsNoTracking() on r.SectionId equals s.Id
                where r.PerformanceIndicatorExamId == examId
                group r by new { r.SectionId, s.Title } into g
                select new SectionPerformanceItem
                {
                    SectionTitle = g.Key.Title,
                    AverageScore = Math.Round(g.Average(x => x.ScorePercent), 1),
                    FailedStudents = g.Count(x => x.ScorePercent < 60)
                }
            ).ToListAsync();

            return new ExamPerformanceDetailViewModel
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                CurriculumTitle = exam.Curriculum?.Title ?? "—",
                Sections = sections
            };
        }

        public async Task<ExamAnalyticsViewModel?> GetExamAnalyticsAsync(int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var exam = await _context.PerformanceIndicatorExams
                .Include(e => e.Curriculum)
                .Include(e => e.Batch)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null) return null;

            // 🔹 جلب المدرب الفعلي المرتبط بالدفعة والمنهج
            var instructorName = await _context.InstructorCurriculumBatches
                .Where(i => i.BatchId == exam.BatchId && i.CurriculumId == exam.CurriculumId)
                .Select(i => i.Instructor.FullName)
                .FirstOrDefaultAsync() ?? "—";

            // 🔹 جلب كل طلاب الدفعة
            var batchStudents = await _context.StudentBatchEnrollments
                .Where(s => s.BatchId == exam.BatchId)
                .Select(s => s.Student)
                .AsNoTracking()
                .ToListAsync();

            var results = await _context.StudentIndicatorResults
                .Include(r => r.Student)
                .Include(r => r.Section)
                .Where(r => r.PerformanceIndicatorExamId == examId)
                .AsNoTracking()
                .ToListAsync();

            int totalStudents = batchStudents.Count;
            int attended = results.Select(r => r.StudentId).Distinct().Count();
            int absent = totalStudents - attended;
            int passed = results.Where(r => r.ScorePercent >= 60).Select(r => r.StudentId).Distinct().Count();
            int failed = attended - passed;

            double successRate = attended == 0 ? 0 : Math.Round((double)passed / attended * 100, 1);

            // 🔹 تحليل المحاور
            var sectionPerformance = results
                .GroupBy(r => r.Section.Title)
                .Select(g => new SectionPerformanceItem
                {
                    SectionTitle = g.Key,
                    AverageScore = Math.Round(g.Average(x => x.ScorePercent), 1),
                    FailedStudents = g.Count(x => x.ScorePercent < 60)
                }).ToList();

            // 🔹 الطلاب المحتاجون خطة علاجية
            var atRiskStudents = results
                .Where(r => r.ScorePercent < 60)
                .GroupBy(r => new { r.StudentId, r.Student.FullName })
                .Select(g => new StudentWeaknessItem
                {
                    StudentId = g.Key.StudentId,
                    StudentName = g.Key.FullName,
                    WeakSections = g.Select(x => x.Section.Title).Distinct().ToList()
                }).ToList();

            return new ExamAnalyticsViewModel
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                BatchName = exam.Batch?.Name ?? "—",
                CurriculumTitle = exam.Curriculum?.Title ?? "—",
                InstructorName = instructorName,
                CreatedAt = exam.CreatedAt,
                TotalStudents = totalStudents,
                PassedStudents = passed,
                FailedStudents = failed,
                AbsentStudents = absent,
                SuccessRate = successRate,
                Sections = sectionPerformance,
                AtRiskStudents = atRiskStudents
            };
        }


        public async Task<StudentRemedialAnalysisViewModel> BuildStudentRemedialAnalysisAsync(int studentId, int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🧩 1. جلب الطالب وربطه بالدفعات والدورات
            var student = await _context.Students
                .Include(s => s.BatchEnrollments)
                    .ThenInclude(e => e.Batch)
                .Include(s => s.StudentCourseEnrollments)
                    .ThenInclude(e => e.Course)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return null;

            // 🧩 2. جلب بيانات اختبار مؤشر الأداء
            var exam = await _context.PerformanceIndicatorExams
                .Include(e => e.Curriculum)
                .Include(e => e.Batch)
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null)
                return null;

            // 🧩 3. تحديد الدفعة والمنهج والمدرب
            var batch = exam.Batch ?? student.BatchEnrollments.FirstOrDefault()?.Batch;
            var curriculum = exam.Curriculum;

            var instructorLink = await _context.InstructorCurriculumBatches
                .Include(icb => icb.Instructor)
                .FirstOrDefaultAsync(icb => icb.BatchId == batch.Id && icb.CurriculumId == curriculum.Id);

            // 🧩 4. جلب نتائج الطالب في اختبار المؤشرات
            var results = await _context.StudentIndicatorResults
                .Include(r => r.Section)
                .Where(r => r.StudentId == studentId && r.PerformanceIndicatorExamId == examId)
                .ToListAsync();

            // 🧩 5. جلب المحاور (Sections) المرتبطة بالمنهج الحالي
            var sections = await _context.Sections
                .Include(s => s.Curriculum)
                .Include(s => s.SectionUnits)
                .Include(s => s.Sessions)
                .Where(s => s.CurriculumId == curriculum.Id)
                .ToListAsync();

            var weakSections = new List<WeakSectionViewModel>();

            foreach (var section in sections)
            {
                // ✅ فقط المحاور الضعيفة (التي فيها أقل من 60%)
                bool isWeak = results.Any(r => r.SectionId == section.Id && r.ScorePercent < 60);
                if (!isWeak) continue;

                // 🧠 المؤشرات (الدروس) الفعالة داخل هذا المحور
                var lessons = await _context.Lessons
                    .Where(l => l.SectionId == section.Id && l.IsActive)
                    .ToListAsync();

                var indicatorList = new List<IndicatorAnalysisViewModel>();

                foreach (var lesson in lessons)
                {
                    // ✅ عدد الأسئلة من الواجبات
                    var homeworkCount = await _context.QuestionAttemptNew
                        .CountAsync(q => q.StudentId == studentId &&
                                         q.Question.LessonId == lesson.Id &&
                                         q.HomeworkSetId != null);

                    // ✅ عدد الأسئلة من الاختبارات
                    var examCount = await _context.QuestionAttemptNew
                        .CountAsync(q => q.StudentId == studentId &&
                                         q.Question.LessonId == lesson.Id &&
                                         q.ExamAssignmentId != null);

                    // ✅ الأسئلة الخاطئة في هذا المؤشر
                    var wrongQuestions = await _context.QuestionAttemptNew
                        .Include(q => q.Question)
                            .ThenInclude(qq => qq.Lesson)
                                .ThenInclude(l => l.Section)
                        .Where(q => q.StudentId == studentId &&
                                    q.Question.LessonId == lesson.Id &&
                                    !q.IsCorrect)
                        .Select(q => new QuestionReviewItem
                        {
                            QuestionText = q.Question.Title,
                            StudentAnswer = q.SelectedAnswer,
                            CorrectAnswer = q.Question.CorrectAnswer,
                            SourceType = q.ExamAssignmentId != null ? "اختبار" :
                                         q.HomeworkSetId != null ? "واجب" : "أخرى",
                            SourceTitle = q.Question.Lesson.Section.Title
                        })
                        .ToListAsync();

                    indicatorList.Add(new IndicatorAnalysisViewModel
                    {
                        Id = lesson.Id,
                        Title = lesson.Title,
                        IsActive = lesson.IsActive,
                        HomeworkQuestionCount = homeworkCount,
                        ExamQuestionCount = examCount,
                        WrongQuestions = wrongQuestions
                    });
                }

                // 🟩 المحور الضعيف النهائي
                weakSections.Add(new WeakSectionViewModel
                {
                    SectionTitle = section.Title,
                    FailedIndicators = lessons
                        .Where(l => l.IsActive)
                        .Select(l => l.Title)
                        .ToList(),
                    Indicators = indicatorList
                });
            }

            // 🟦 7. تجميع البيانات النهائية للعرض
            return new StudentRemedialAnalysisViewModel
            {
                StudentId = student.StudentID,
                StudentName = student.FullName,
                BatchName = batch?.Name ?? "—",
                CurriculumTitle = curriculum?.Title ?? "—",
                InstructorName = instructorLink?.Instructor?.FullName ?? "غير محدد",
                WeakSections = weakSections
            };
        }


    }
}
