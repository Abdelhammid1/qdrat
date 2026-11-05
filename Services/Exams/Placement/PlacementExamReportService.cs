using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Reports;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Partner;
using QdratNew.ViewModels.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QdratNew.Services.Exams.Placement
{
    // PPL-S3: منطق تقارير Admin/PlacementExams منقول كما هو، مع فلترة النطاق.
    // نص الرسائل وأرقام التقارير لنطاق الأدمن مطابقة لما كان في الـ Controller (انظر PlacementExamReportSnapshotTests).
    public sealed class PlacementExamReportService : IPlacementExamReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPlacementExamScopeGuard _guard;
        private readonly ITimeZoneService _timeZoneService;
        private readonly IExamRecommendationService _recommendationService;
        private readonly ReportBrowserProvider _reportBrowserProvider;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<PlacementExamReportService> _logger;

        private const string PartnerReportCollaborationText = "بالتعاون مع معهد القدرات";

        public PlacementExamReportService(
            ApplicationDbContext context,
            IPlacementExamScopeGuard guard,
            ITimeZoneService timeZoneService,
            IExamRecommendationService recommendationService,
            ReportBrowserProvider reportBrowserProvider,
            IWebHostEnvironment environment,
            ILogger<PlacementExamReportService> logger)
        {
            _context = context;
            _guard = guard;
            _timeZoneService = timeZoneService;
            _recommendationService = recommendationService;
            _reportBrowserProvider = reportBrowserProvider;
            _environment = environment;
            _logger = logger;
        }

        // صفوف ربط الاختبار بالدفعات؛ الشريك يرى دفعاته فقط حتى لو ارتبط الاختبار بغيرها
        private IQueryable<ExamAssignmentToBatch> BatchLinks(PlacementScope scope)
        {
            var links = _context.ExamAssignmentsToBatches.AsNoTracking();
            if (!scope.IsPartner)
                return links;

            var partnerId = scope.PartnerId!.Value;
            return links.Where(x => x.Batch.Branch.PartnerId == partnerId);
        }

        // APD-S3.1: اختبار الشريك قد يرتبط بعدة دفعات، فتُختار دفعة الطالب لا أول دفعة. الأدمن بلا تغيير.
        private IQueryable<ExamAssignmentToBatch> StudentBatchLinks(PlacementScope scope, int studentId)
            => scope.IsPartner
                ? BatchLinks(scope).Where(l => _context.StudentBatchEnrollments.Any(e => e.BatchId == l.BatchId && e.StudentID == studentId))
                : BatchLinks(scope);

        // PPL-S6: الشريك لا يفتح تقرير دفعة لاختبار لم يُربط بها
        private async Task<bool> IsPartnerExamNotOnBatchAsync(PlacementScope scope, int examId, int batchId, CancellationToken ct)
            => scope.IsPartner &&
               !await BatchLinks(scope).AnyAsync(x => x.ExamId == examId && x.BatchId == batchId, ct);

        // =====================================================
        // PPL-S6: ترويسة تقارير الشريك + اختبارات الطالب
        // =====================================================
        public async Task<PartnerPlacementBrandingVm?> GetReportBrandingAsync(PlacementScope scope, CancellationToken ct = default)
        {
            if (!scope.IsPartner)
                return null;

            var partnerId = scope.PartnerId!.Value;
            var partner = await _context.Partners
                .AsNoTracking()
                .Where(p => p.Id == partnerId)
                .Select(p => new { p.Name, p.LogoPath })
                .FirstOrDefaultAsync(ct);

            if (partner == null)
                return null;

            return new PartnerPlacementBrandingVm
            {
                PartnerName = partner.Name,
                LogoUrl = GetPartnerLogoFilePath(partner.LogoPath) == null ? null : partner.LogoPath
            };
        }

        // الشعار من مجلد الشركاء فقط؛ PDF يقرأ الملف من القرص فلا يُمرَّر له مسار غيره
        private string? GetPartnerLogoFilePath(string? logoPath)
        {
            const string partnersFolder = "/uploads/partners/";

            if (string.IsNullOrWhiteSpace(logoPath) ||
                !logoPath.StartsWith(partnersFolder, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(_environment.WebRootPath))
                return null;

            var fileName = Path.GetFileName(logoPath);
            if (string.IsNullOrEmpty(fileName))
                return null;

            var filePath = Path.Combine(_environment.WebRootPath, "uploads", "partners", fileName);
            return File.Exists(filePath) ? filePath : null;
        }

        public async Task<List<PartnerPlacementStudentExamVm>> GetStudentPlacementExamsAsync(PlacementScope scope, int studentId, CancellationToken ct = default)
        {
            if (!await _guard.CanAccessStudentResultsAsync(_context, scope, studentId, ct))
                return new List<PartnerPlacementStudentExamVm>();

            var rows = await (
                from a in _context.ExamAssignments.AsNoTracking()
                join e in _guard.PlacementExams(_context, scope).AsNoTracking() on a.ExamId equals e.Id
                where a.StudentId == studentId
                select new
                {
                    a.ExamId,
                    e.Title,
                    AssignmentId = a.Id,
                    a.AssignedAt,
                    BatchId = StudentBatchLinks(scope, studentId)
                        .Where(l => l.ExamId == a.ExamId)
                        .Select(l => (int?)l.BatchId)
                        .FirstOrDefault(),
                    Status = _context.ExamStudentStatuses
                        .Where(s => s.StudentId == studentId && s.ExamId == a.ExamId)
                        .Select(s => new { s.IsSubmitted, s.Status, s.Score })
                        .FirstOrDefault()
                }
            ).ToListAsync(ct);

            return rows
                .GroupBy(x => x.ExamId)
                .Select(g => g.OrderByDescending(x => x.AssignedAt).First())
                .OrderByDescending(x => x.AssignedAt)
                .Select(x => new PartnerPlacementStudentExamVm
                {
                    ExamId = x.ExamId,
                    Title = x.Title,
                    AssignmentId = x.AssignmentId,
                    BatchId = x.BatchId ?? 0,
                    AssignedAt = x.AssignedAt,
                    IsSubmitted = x.Status != null && (x.Status.IsSubmitted || x.Status.Status == ExamStatus.Completed),
                    Score = x.Status?.Score
                })
                .ToList();
        }

        // =====================================================
        // BatchPerformanceReport
        // =====================================================
        public async Task<PlacementReportResult<AdminExamSectionAnalysisViewModel>> GetBatchPerformanceReportAsync(PlacementScope scope, int examId, int batchId, CancellationToken ct = default)
        {
            // 🟦 1. جلب بيانات الدفعة والاختبار
            var exam = await _guard.ExamsInScope(_context, scope).AsNoTracking().FirstOrDefaultAsync(e => e.Id == examId, ct);
            var batch = await _guard.Batches(_context, scope).AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId, ct);
            if (exam == null || batch == null || await IsPartnerExamNotOnBatchAsync(scope, examId, batchId, ct))
                return PlacementReportResult<AdminExamSectionAnalysisViewModel>.NotFound("❌ لم يتم العثور على بيانات الدفعة أو الاختبار.");

            // 🟩 2. جلب الأسئلة والمحاور
            // PPL-S7.2: الأعمدة المطلوبة فقط (عناوين الأسئلة قد تكون HTML طويلًا ولا يستخدمها التقرير)
            var examQuestions = await _context.ExamQuestions
                .AsNoTracking()
                .Where(eq => eq.ExamId == examId)
                .Select(eq => new
                {
                    eq.QuestionId,
                    eq.Question.LessonId,
                    LessonTitle = eq.Question.Lesson.Title,
                    eq.Question.Lesson.SectionId,
                    SectionTitle = eq.Question.Lesson.Section.Title
                })
                .ToListAsync(ct);

            if (!examQuestions.Any())
                return PlacementReportResult<AdminExamSectionAnalysisViewModel>.NoData("⚠️ لا توجد أسئلة مرتبطة بهذا الاختبار.");

            // 🟨 3. جلب كل محاولات طلاب الدفعة فقط
            // PPL-S7.2: تجميع داخل SQL؛ دفعة كبيرة كانت تُحمّل كل محاولة (طلاب × أسئلة) ثم تُعدّ لكل مؤشر
            var attempts = (await (
                from a in _context.QuestionAttemptNew
                join s in _guard.StudentsWithResults(_context, scope) on a.StudentId equals s.StudentID
                join sb in _context.StudentBatchEnrollments on s.StudentID equals sb.StudentID
                where a.ExamId == examId && sb.BatchId == batchId
                group a by new { a.QuestionId, a.IsCorrect } into g
                select new
                {
                    g.Key.QuestionId,
                    g.Key.IsCorrect,
                    Count = g.Count()
                }
            ).ToListAsync(ct))
            .ToLookup(a => a.QuestionId);

            // 🧮 4. بناء تقرير الأداء لكل محور
            var grouped = examQuestions
                .GroupBy(eq => new { eq.SectionId, eq.SectionTitle })
                .Select(secGroup => new
                {
                    secGroup.Key.SectionId,
                    secGroup.Key.SectionTitle,
                    Lessons = secGroup.GroupBy(x => new { x.LessonId, x.LessonTitle })
                        .Select(lessonGroup =>
                        {
                            var questionIds = lessonGroup.Select(x => x.QuestionId).ToList();
                            var total = questionIds.Count;
                            var lessonAttempts = questionIds.Distinct().SelectMany(id => attempts[id]).ToList();
                            var correctCount = lessonAttempts.Where(a => a.IsCorrect).Sum(a => a.Count);
                            var wrongCount = lessonAttempts.Where(a => !a.IsCorrect).Sum(a => a.Count);
                            var totalAttempts = correctCount + wrongCount;
                            var percent = totalAttempts > 0 ? Math.Round(correctCount * 100.0 / totalAttempts, 1) : 0.0;

                            return new LessonPerformanceEntry
                            {
                                LessonId = lessonGroup.Key.LessonId,
                                LessonTitle = lessonGroup.Key.LessonTitle,
                                TotalQuestions = total,
                                Correct = correctCount,
                                Wrong = wrongCount,
                                Score = (int)percent
                            };
                        }).ToList()
                }).ToList();

            // 🧾 5. إنشاء ViewModel للعرض
            return PlacementReportResult<AdminExamSectionAnalysisViewModel>.Ok(new AdminExamSectionAnalysisViewModel
            {
                ExamId = examId,
                AssignmentId = 0,
                StudentId = 0,
                ExamTitle = $"{exam.Title} - {batch.Name}",
                StudentName = $"دفعة {batch.Name}",
                SectionReports = grouped.Select(s => new SectionPerformanceEntry
                {
                    SectionId = s.SectionId,
                    SectionTitle = s.SectionTitle,
                    TotalQuestions = s.Lessons.Sum(l => l.TotalQuestions),
                    Correct = s.Lessons.Sum(l => l.Correct),
                    Wrong = s.Lessons.Sum(l => l.Wrong),
                    Score = s.Lessons.Sum(l => l.Correct + l.Wrong) > 0
                        ? (int)(s.Lessons.Sum(l => l.Correct) * 100.0 / s.Lessons.Sum(l => l.Correct + l.Wrong))
                        : 0,
                    LessonBreakdown = s.Lessons
                }).ToList()
            });
        }

        // =====================================================
        // ReviewLessonQuestionsBatch
        // =====================================================
        public async Task<BatchLessonQuestionsReportViewModel?> GetReviewLessonQuestionsBatchAsync(PlacementScope scope, int examId, int lessonId, CancellationToken ct = default, int batchId = 0)
        {
            var exam = await _guard.ExamsInScope(_context, scope).AsNoTracking().FirstOrDefaultAsync(e => e.Id == examId, ct);
            if (exam == null) return null;

            var lesson = await _context.Lessons
                .AsNoTracking()
                .Include(l => l.Section)
                .FirstOrDefaultAsync(l => l.Id == lessonId, ct);
            if (lesson == null) return null;

            var examLessonQuestions = _context.Questions
                .AsNoTracking()
                .Where(q => q.LessonId == lessonId &&
                            _context.ExamQuestions.Any(eq => eq.ExamId == examId && eq.QuestionId == q.Id));

            // الشريك: لا يقرأ عنوان درس ليس ضمن اختباره
            if (scope.IsPartner && !await examLessonQuestions.AnyAsync(ct))
                return null;

            // APD-S3.1: اختبار الشريك قد يرتبط بعدة دفعات، فتحدد الصفحة دفعتها إن كانت مرتبطة بالاختبار
            var requestedBatchId = batchId;
            var batchLinks = BatchLinks(scope).Where(e => e.ExamId == examId);
            if (requestedBatchId > 0)
                batchLinks = batchLinks.OrderByDescending(e => e.BatchId == requestedBatchId);

            batchId = await batchLinks
                .Select(e => e.BatchId)
                .FirstOrDefaultAsync(ct);

            var students = _guard.StudentsWithResults(_context, scope);

            int totalBatchStudents = await (
                from sb in _context.StudentBatchEnrollments
                join s in students on sb.StudentID equals s.StudentID
                where sb.BatchId == batchId
                select sb.StudentID
            ).CountAsync(ct);

            var questions = await examLessonQuestions
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .ToListAsync(ct);

            var attempts = await (
                from a in _context.QuestionAttemptNew
                join s in students on a.StudentId equals s.StudentID
                join sb in _context.StudentBatchEnrollments on s.StudentID equals sb.StudentID
                where a.ExamId == examId && sb.BatchId == batchId
                select new { a.QuestionId, a.IsCorrect, a.SelectedAnswer, a.TimeTakenSeconds }
            ).ToListAsync(ct);

            var optionLabels = new[] { "أ", "ب", "ج", "د" };

            var entries = questions.Select(q =>
            {
                var qa = attempts.Where(a => a.QuestionId == q.Id).ToList();
                int total = qa.Count;
                int correct = qa.Count(a => a.IsCorrect);
                int wrong = total - correct;
                int skipped = Math.Max(0, totalBatchStudents - total);
                double percent = total > 0 ? Math.Round(correct * 100.0 / total, 1) : 0.0;
                double avgTime = total > 0 ? Math.Round(qa.Average(a => a.TimeTakenSeconds), 1) : 0.0;

                var optionEntries = q.Options
                    .Select((opt, idx) =>
                    {
                        int selCount = qa.Count(a =>
                            !string.IsNullOrWhiteSpace(a.SelectedAnswer) &&
                            a.SelectedAnswer.Trim() == opt.Text?.Trim());
                        return new BatchQuestionOptionEntry
                        {
                            Label = idx < optionLabels.Length ? optionLabels[idx] : (idx + 1).ToString(),
                            Text = opt.Text,
                            ImageUrl = opt.ImageUrl,
                            IsCorrect = !string.IsNullOrWhiteSpace(q.CorrectAnswer)
                                && opt.Text?.Trim() == q.CorrectAnswer.Trim(),
                            SelectedCount = selCount,
                            SelectionPercent = total > 0
                                ? Math.Round(selCount * 100.0 / total, 1)
                                : 0.0
                        };
                    }).ToList();

                return new BatchLessonQuestionEntry
                {
                    QuestionId = q.Id,
                    QuestionTitle = q.Title,
                    ImageUrl = q.ImageUrl,
                    Template = q.Template,
                    ValueA = q.ValueA,
                    ValueB = q.ValueB,
                    HasVerbalPassage = q.VerbalPassage != null,
                    VerbalPassageTitle = q.VerbalPassage?.Title,
                    VerbalPassageContent = q.VerbalPassage?.Content,
                    VerbalPassageType = q.VerbalPassage?.Type ?? PassageType.Text,
                    VerbalPassageMediaUrl = q.VerbalPassage?.MediaUrl,
                    TotalAnswers = total,
                    CorrectAnswers = correct,
                    WrongAnswers = wrong,
                    SkippedCount = skipped,
                    Percent = percent,
                    AvgTimeTakenSeconds = avgTime,
                    Options = optionEntries
                };
            }).ToList();

            return new BatchLessonQuestionsReportViewModel
            {
                ExamTitle = exam.Title,
                LessonTitle = lesson.Title,
                SectionTitle = lesson.Section.Title,
                TotalQuestions = entries.Count,
                TotalCorrect = entries.Sum(e => e.CorrectAnswers),
                TotalWrong = entries.Sum(e => e.WrongAnswers),
                OverallPercent = entries.Sum(e => e.CorrectAnswers + e.WrongAnswers) > 0
                    ? Math.Round(entries.Sum(e => e.CorrectAnswers) * 100.0 /
                                 entries.Sum(e => e.CorrectAnswers + e.WrongAnswers), 1)
                    : 0,
                Questions = entries
            };
        }

        // =====================================================
        // ExamSectionAnalysis
        // =====================================================
        public async Task<PlacementReportResult<AdminExamSectionAnalysisViewModel>> GetSectionAnalysisAsync(PlacementScope scope, int assignmentId, int studentId, CancellationToken ct = default)
        {
            const string notFoundMessage = "❌ لم يتم العثور على هذا الاختبار أو الطالب.";

            // ✅ جلب بيانات الاختبار
            var exams = _guard.ExamsInScope(_context, scope);
            var assignment = await _context.ExamAssignments
                .AsNoTracking()
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId && a.StudentId == studentId && exams.Any(e => e.Id == a.ExamId), ct);

            if (assignment == null || !await _guard.CanAccessStudentResultsAsync(_context, scope, studentId, ct))
                return PlacementReportResult<AdminExamSectionAnalysisViewModel>.NotFound(notFoundMessage);

            // ✅ جلب الأسئلة الخاصة بالاختبار
            var examQuestions = await _context.ExamQuestions
                .AsNoTracking()
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                .Where(eq => eq.ExamId == assignment.ExamId)
                .ToListAsync(ct);

            // ✅ جلب محاولات الطالب في نفس الاختبار
            var attempts = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && a.ExamId == assignment.ExamId)
                .ToListAsync(ct);

            // ✅ تجميع بيانات الأسئلة حسب المحور والمؤشر
            var data = examQuestions.Select(eq =>
            {
                var attempt = attempts.FirstOrDefault(a => a.QuestionId == eq.QuestionId);
                return new
                {
                    SectionId = eq.Question.Lesson.SectionId,
                    SectionTitle = eq.Question.Lesson.Section.Title,
                    LessonId = eq.Question.LessonId,
                    LessonTitle = eq.Question.Lesson.Title,
                    IsCorrect = attempt != null && attempt.IsCorrect
                };
            }).ToList();

            // 🧮 تجميع حسب المحور
            var grouped = data.GroupBy(d => new { d.SectionId, d.SectionTitle })
                .Select(g => new
                {
                    g.Key.SectionId,
                    g.Key.SectionTitle,
                    Lessons = g.GroupBy(x => x.LessonTitle)
                               .Select(lg => new
                               {
                                   lg.First().LessonId,
                                   LessonTitle = lg.Key,
                                   Total = lg.Count(),
                                   Correct = lg.Count(x => x.IsCorrect),
                                   Wrong = lg.Count(x => !x.IsCorrect),
                                   Score = lg.Count() > 0 ? (int)(lg.Count(x => x.IsCorrect) * 100.0 / lg.Count()) : 0
                               }).ToList()
                }).ToList();

            var studentName = await _context.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync(ct);

            // ✅ بناء الـ ViewModel النهائي
            return PlacementReportResult<AdminExamSectionAnalysisViewModel>.Ok(new AdminExamSectionAnalysisViewModel
            {
                AssignmentId = assignmentId,
                ExamId = assignment.ExamId,
                StudentId = studentId,
                ExamTitle = assignment.Exam?.Title ?? "اختبار غير معروف",
                StudentName = studentName,
                SectionReports = grouped.Select(s => new SectionPerformanceEntry
                {
                    SectionId = s.SectionId,
                    SectionTitle = s.SectionTitle,
                    TotalQuestions = s.Lessons.Sum(l => l.Total),
                    Correct = s.Lessons.Sum(l => l.Correct),
                    Wrong = s.Lessons.Sum(l => l.Wrong),
                    Score = s.Lessons.Sum(l => l.Total) > 0
                        ? (int)(s.Lessons.Sum(l => l.Correct) * 100.0 / s.Lessons.Sum(l => l.Total))
                        : 0,
                    LessonBreakdown = s.Lessons.Select(l => new LessonPerformanceEntry
                    {
                        LessonId = l.LessonId,
                        LessonTitle = l.LessonTitle,
                        TotalQuestions = l.Total,
                        Correct = l.Correct,
                        Wrong = l.Wrong,
                        Score = l.Score
                    }).ToList()
                }).ToList()
            });
        }

        // =====================================================
        // ReviewLessonQuestions
        // =====================================================
        public async Task<PlacementReportResult<ExamReviewViewModel>> GetReviewLessonQuestionsAsync(PlacementScope scope, int assignmentId, int studentId, int lessonId, CancellationToken ct = default)
        {
            const string assignmentNotFoundMessage = "❌ لم يتم العثور على هذا الاختبار.";

            // ✅ 1. جلب بيانات التعيين والاختبار
            var exams = _guard.ExamsInScope(_context, scope);
            var assignment = await _context.ExamAssignments
                .AsNoTracking()
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId && exams.Any(e => e.Id == a.ExamId), ct);

            if (assignment == null)
                return PlacementReportResult<ExamReviewViewModel>.NotFound(assignmentNotFoundMessage);

            // الشريك: التعيين يجب أن يخص نفس الطالب، والطالب من فروعه
            if (scope.IsPartner &&
                (assignment.StudentId != studentId || !await _guard.CanAccessStudentResultsAsync(_context, scope, studentId, ct)))
                return PlacementReportResult<ExamReviewViewModel>.NotFound(assignmentNotFoundMessage);

            var examId = assignment.ExamId;

            // ✅ 2. جلب بيانات المؤشر (الدرس)
            var lesson = await _context.Lessons
                .AsNoTracking()
                .Include(l => l.Section)
                .FirstOrDefaultAsync(l => l.Id == lessonId, ct);

            if (lesson == null)
                return PlacementReportResult<ExamReviewViewModel>.NotFound("❌ لم يتم العثور على هذا المؤشر.");

            // ✅ 3. جلب الأسئلة الخاصة بالمؤشر داخل هذا الاختبار
            var questions = await (
                from eq in _context.ExamQuestions
                join q in _context.Questions on eq.QuestionId equals q.Id
                where eq.ExamId == examId && q.LessonId == lessonId
                select new
                {
                    q.Id,
                    q.Title,
                    q.ImageUrl,
                    q.CorrectAnswer,
                    q.IsQuantitative
                }
            ).ToListAsync(ct);

            if (!questions.Any())
                return PlacementReportResult<ExamReviewViewModel>.NoData("⚠️ لا توجد أسئلة لهذا المؤشر في هذا الاختبار.");

            // ✅ 4. جلب محاولات الطالب على هذه الأسئلة
            var attempts = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && a.ExamId == examId)
                .ToListAsync(ct);

            var optionsByQuestion = (await _context.QuestionOptions
                .AsNoTracking()
                .Where(o => _context.ExamQuestions.Any(eq =>
                    eq.ExamId == examId && eq.QuestionId == o.QuestionId && eq.Question.LessonId == lessonId))
                .OrderBy(o => o.Id)
                .Select(o => new { o.QuestionId, o.Text, o.ImageUrl })
                .ToListAsync(ct))
                .ToLookup(o => o.QuestionId);

            // ✅ 5. بناء نموذج العرض
            return PlacementReportResult<ExamReviewViewModel>.Ok(new ExamReviewViewModel
            {
                AssignmentId = assignmentId,
                ExamTitle = $"{assignment.Exam.Title} - {lesson.Title}",
                ExamAssignmentId = assignment.Id,
                TotalQuestions = questions.Count,
                TimeSpentMinutes = 0,
                Questions = questions.Select(q =>
                {
                    var attempt = attempts.FirstOrDefault(a => a.QuestionId == q.Id);

                    return new ExamReviewQuestionVm
                    {
                        QuestionId = q.Id,
                        QuestionTitle = q.Title,
                        ImageUrl = q.ImageUrl,
                        CorrectAnswer = q.CorrectAnswer,
                        StudentAnswer = attempt?.SelectedAnswer ?? "—",
                        IsCorrect = attempt?.IsCorrect ?? false,
                        TimeTakenSeconds = attempt?.TimeTakenSeconds ?? 0,
                        DisplayType = QuestionDisplayType.TextOnly, // ✅ القيمة الافتراضية الآمنة
                        IsQuantitative = q.IsQuantitative,
                        Options = optionsByQuestion[q.Id]
                            .Select(o => new QdratNew.ViewModels.Homework.QuestionOptionVm
                            {
                                Text = o.Text,
                                ImageUrl = o.ImageUrl
                            })
                            .ToList()
                    };
                }).ToList()
            });
        }

        // =====================================================
        // BatchStudents
        // =====================================================
        public async Task<PlacementBatchStudentsData?> GetBatchStudentsAsync(PlacementScope scope, int examId, int batchId, CancellationToken ct = default)
        {
            if (await _guard.IsExamDeniedAsync(_context, scope, examId, ct) ||
                !await _guard.CanAccessBatchAsync(_context, scope, batchId, ct) ||
                await IsPartnerExamNotOnBatchAsync(scope, examId, batchId, ct))
                return null;

            var batchName = await _context.Batches
                .Where(b => b.Id == batchId && b.IsActive)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(ct);

            var examTitle = await _context.Exams
                .Where(e => e.Id == examId)
                .Select(e => e.Title)
                .FirstOrDefaultAsync(ct);

            // 🧮 عدد أسئلة الاختبار — يُستخدم لاعتبار الطالب "منتهيًا" إذا أجاب على كل الأسئلة
            // حتى لو لم يُستدعَ SubmitFinal (مثل إغلاق المتصفح بعد آخر سؤال أو انقطاع الشبكة)
            var totalQuestions = await _context.ExamQuestions
                .CountAsync(eq => eq.ExamId == examId, ct);

            var studentsInScope = _guard.StudentsWithResults(_context, scope);

            var rawStudents = await (
                from s in studentsInScope
                join sb in _context.StudentBatchEnrollments on s.StudentID equals sb.StudentID
                join ea in _context.ExamAssignments
                    .Where(e => e.ExamId == examId)
                    on s.StudentID equals ea.StudentId into studentAssignments
                from lastEa in studentAssignments
                    .OrderByDescending(x => x.AssignedAt)
                    .Take(1)
                    .DefaultIfEmpty()
                where sb.BatchId == batchId
                select new
                {
                    s.StudentID,
                    s.FullName,
                    // ⚠️ إن لم يوجد سجل ExamAssignments لهذا الطالب (مثلاً انضم للدفعة بعد الإرسال الأول)
                    // نحاول استخراج رقم التعيين الحقيقي من محاولاته المسجلة فعليًا على هذا الاختبار
                    AssignmentId = lastEa != null ? lastEa.Id :
                        (_context.QuestionAttemptNew
                            .Where(a => a.StudentId == s.StudentID &&
                                        a.ExamId == examId &&
                                        a.ExamAssignmentId != null)
                            .Select(a => a.ExamAssignmentId)
                            .FirstOrDefault() ?? 0),

                    IsStatusCompleted = _context.ExamStudentStatuses
                        .Any(st =>
                            st.StudentId == s.StudentID &&
                            st.ExamId == examId &&
                            (st.Status == ExamStatus.Completed || st.IsSubmitted)),

                    ExamStatus = _context.ExamStudentStatuses
                        .Where(st =>
                            st.StudentId == s.StudentID &&
                            st.ExamId == examId)
                        .Select(st => st.Status)
                        .FirstOrDefault(),

                    AnsweredCount = _context.QuestionAttemptNew
                        .Count(a => a.StudentId == s.StudentID &&
                            (a.ExamId == examId || (lastEa != null && a.ExamAssignmentId == lastEa.Id))),

                    CorrectCount = _context.QuestionAttemptNew
                        .Count(a => a.StudentId == s.StudentID &&
                            (a.ExamId == examId || (lastEa != null && a.ExamAssignmentId == lastEa.Id)) &&
                            a.IsCorrect)
                }
            ).ToListAsync(ct);

            // الـ fallback يقرأ تعيينات أي اختبار في الدفعة، لذا يُقيَّد بنطاق الاختبارات حتى لا تتسرب نتائج اختبار نطاق آخر
            var fallbackAssignments = await (
                from sb in _context.StudentBatchEnrollments
                join st in studentsInScope on sb.StudentID equals st.StudentID
                join ea in _context.ExamAssignments on sb.StudentID equals ea.StudentId
                join eab in _context.ExamAssignmentsToBatches on ea.ExamId equals eab.ExamId
                join ex in _guard.ExamsInScope(_context, scope) on ea.ExamId equals ex.Id
                where sb.BatchId == batchId && eab.BatchId == batchId
                select new
                {
                    StudentId = sb.StudentID,
                    AssignmentId = ea.Id,
                    ea.ExamId,
                    ea.AssignedAt,
                    HasAttempts = _context.QuestionAttemptNew.Any(a =>
                        a.StudentId == sb.StudentID &&
                        (a.ExamId == ea.ExamId || a.ExamAssignmentId == ea.Id)),
                    IsCompleted = _context.ExamStudentStatuses.Any(st =>
                        st.StudentId == sb.StudentID &&
                        (st.ExamId == ea.ExamId || st.ExamAssignmentId == ea.Id) &&
                        (st.Status == ExamStatus.Completed || st.IsSubmitted)),
                    TotalQuestions = _context.ExamQuestions.Count(eq => eq.ExamId == ea.ExamId),
                    AnsweredCount = _context.QuestionAttemptNew.Count(a =>
                        a.StudentId == sb.StudentID &&
                        (a.ExamId == ea.ExamId || a.ExamAssignmentId == ea.Id)),
                    CorrectCount = _context.QuestionAttemptNew.Count(a =>
                        a.StudentId == sb.StudentID &&
                        (a.ExamId == ea.ExamId || a.ExamAssignmentId == ea.Id) &&
                        a.IsCorrect)
                }
            ).ToListAsync(ct);

            var fallbackAssignmentByStudent = fallbackAssignments
                .GroupBy(x => x.StudentId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(x => x.HasAttempts)
                        .ThenByDescending(x => x.IsCompleted)
                        .ThenByDescending(x => x.AssignedAt)
                        .First());

            // 🛡️ Translation Guard: طلاب موقوفون حاليًا على اختبار تحديد المستوى هذا
            // PPL-S7.2: طلاب هذه الدفعة فقط، لا كل المخالفات المفتوحة في المنصة
            var blockedPlacementKeys = (await _context.IntegrityViolationLogs
                .Where(v => v.AttemptType == IntegrityAttemptType.Placement && !v.IsResolved &&
                            _context.StudentBatchEnrollments.Any(sb => sb.BatchId == batchId && sb.StudentID == v.StudentId))
                .Select(v => new { v.StudentId, v.AttemptEntityId })
                .ToListAsync(ct))
                .Select(v => (v.StudentId, v.AttemptEntityId))
                .ToHashSet();

            // ⚖️ نفس منطق تحديد "الحل" المستخدم في تقرير الطالب (PlacementReport):
            // إما أن يكون هناك سجل حالة مكتمل صراحةً، أو أن الطالب أجاب على كل الأسئلة فعليًا
            var students = rawStudents.Select(s =>
            {
                fallbackAssignmentByStudent.TryGetValue(s.StudentID, out var fallbackAssignment);
                var reportAssignmentId = s.AssignmentId;

                if (fallbackAssignment != null &&
                    (reportAssignmentId == 0 || (s.AnsweredCount == 0 && fallbackAssignment.HasAttempts)))
                {
                    reportAssignmentId = fallbackAssignment.AssignmentId;
                }

                var displayAnsweredCount = s.AnsweredCount > 0
                    ? s.AnsweredCount
                    : fallbackAssignment?.AnsweredCount ?? 0;

                var displayCorrectCount = s.AnsweredCount > 0
                    ? s.CorrectCount
                    : fallbackAssignment?.CorrectCount ?? 0;

                var displayTotalQuestions = s.AnsweredCount > 0
                    ? totalQuestions
                    : fallbackAssignment?.TotalQuestions ?? totalQuestions;

                bool hasFullAttempts = displayTotalQuestions > 0 && displayAnsweredCount >= displayTotalQuestions;
                bool isSubmitted = s.IsStatusCompleted || (fallbackAssignment?.IsCompleted ?? false) || hasFullAttempts;

                return new PlacementExamStudentVm
                {
                    StudentId = s.StudentID,
                    StudentName = s.FullName,
                    AssignmentId = reportAssignmentId,
                    IsSubmitted = isSubmitted,
                    ExamStatus = isSubmitted ? ExamStatus.Completed : s.ExamStatus,
                    Score = displayAnsweredCount > 0
                        ? Math.Round(displayCorrectCount * 100.0 / displayAnsweredCount, 1)
                        : 0,
                    IsIntegrityBlocked = blockedPlacementKeys.Contains((s.StudentID, reportAssignmentId))
                };
            }).ToList();

            return new PlacementBatchStudentsData
            {
                BatchName = batchName,
                ExamTitle = examTitle,
                Students = students
            };
        }

        // =====================================================
        // StudentExamDetails
        // =====================================================
        public async Task<StudentExamDetailsVm?> GetStudentExamDetailsAsync(PlacementScope scope, int examId, int studentId, CancellationToken ct = default)
        {
            var student = await _guard.StudentsWithResults(_context, scope)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentID == studentId, ct);

            var exam = await _guard.ExamsInScope(_context, scope)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == examId, ct);

            if (student == null || exam == null)
                return null;

            // 🔹 جلب بيانات الأسئلة وإجابات الطالب
            var answers = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                where a.StudentId == studentId && a.ExamId == examId
                select new
                {
                    q.Title,
                    q.CorrectAnswer,
                    a.SelectedAnswer,
                    a.IsCorrect
                }
            ).ToListAsync(ct);

            return new StudentExamDetailsVm
            {
                StudentId = student.StudentID,
                ExamId = examId,

                StudentName = student.FullName,
                ExamTitle = exam.Title,
                TotalQuestions = exam.TotalQuestions,
                CorrectCount = answers.Count(x => x.IsCorrect),
                WrongCount = answers.Count(x => !x.IsCorrect),
                Answers = answers.Select(x => new StudentExamAnswerVm
                {
                    QuestionTitle = x.Title,
                    SelectedAnswer = x.SelectedAnswer,
                    CorrectAnswer = x.CorrectAnswer,
                    IsCorrect = x.IsCorrect
                }).ToList()
            };
        }

        // =====================================================
        // AdminPlacementReport
        // =====================================================
        public async Task<PlacementReportResult<PlacementReportViewModel>> GetStudentPlacementReportAsync(PlacementScope scope, int studentId, int assignmentId, int examId, int batchId, CancellationToken ct = default)
        {
            const string examNotFoundMessage = "❌ اختبار المستوى غير موجود.";

            // ✅ جلب بيانات الطالب
            var student = await _guard.StudentsWithResults(_context, scope)
                .AsNoTracking()
                .Include(s => s.Parent)
                .FirstOrDefaultAsync(s => s.StudentID == studentId, ct);

            if (student == null)
                return PlacementReportResult<PlacementReportViewModel>.NotFound("لم يتم العثور على بيانات الطالب.");

            if (batchId > 0 && !await _guard.CanAccessBatchAsync(_context, scope, batchId, ct))
                return PlacementReportResult<PlacementReportViewModel>.NotFound(examNotFoundMessage);

            var exams = _guard.ExamsInScope(_context, scope);

            // ✅ جلب بيانات التعيين والاختبار
            var assignment = assignmentId > 0
                ? await _context.ExamAssignments
                    .AsNoTracking()
                    .Include(a => a.Exam)
                    .FirstOrDefaultAsync(a => a.Id == assignmentId && a.StudentId == studentId && exams.Any(e => e.Id == a.ExamId), ct)
                : null;

            // اختبار نطاق آخر: لا نبحث عن بديل له في الدفعة
            if (assignment == null && examId > 0 && await _guard.IsExamDeniedAsync(_context, scope, examId, ct))
                return PlacementReportResult<PlacementReportViewModel>.NotFound(examNotFoundMessage);

            if (assignment == null && examId > 0 && batchId <= 0)
            {
                batchId = await (
                    from eab in BatchLinks(scope)
                    join sbe in _context.StudentBatchEnrollments on eab.BatchId equals sbe.BatchId
                    where eab.ExamId == examId && sbe.StudentID == studentId
                    select eab.BatchId
                ).FirstOrDefaultAsync(ct);
            }

            // ⚠️ إن لم يصل assignmentId صحيحًا (مثلاً 0) نبحث أولًا عن التعيين الذي عليه إجابات
            // داخل نفس الدفعة، لأن تقرير الطالب يعتمد على التعيين الحقيقي لا على ExamId المعروض فقط.
            if (assignment == null && batchId > 0)
            {
                var realAssignmentId = await (
                    from ea in _context.ExamAssignments
                    join eab in _context.ExamAssignmentsToBatches on ea.ExamId equals eab.ExamId
                    join ex in exams on ea.ExamId equals ex.Id
                    where ea.StudentId == studentId &&
                          eab.BatchId == batchId &&
                          _context.QuestionAttemptNew.Any(a =>
                              a.StudentId == studentId &&
                              (a.ExamId == ea.ExamId || a.ExamAssignmentId == ea.Id))
                    orderby ea.AssignedAt descending
                    select ea.Id
                ).FirstOrDefaultAsync(ct);

                if (realAssignmentId > 0)
                {
                    assignment = await _context.ExamAssignments
                        .AsNoTracking()
                        .Include(a => a.Exam)
                        .FirstOrDefaultAsync(a => a.Id == realAssignmentId && a.StudentId == studentId, ct);
                }
            }

            // ⚠️ fallback قديم: لو لا توجد دفعة في الرابط، حاول نفس الاختبار مباشرة.
            if (assignment == null && examId > 0)
            {
                var realAssignmentId = await _context.QuestionAttemptNew
                    .Where(a => a.StudentId == studentId &&
                                a.ExamId == examId &&
                                a.ExamAssignmentId != null)
                    .Select(a => a.ExamAssignmentId!.Value)
                    .FirstOrDefaultAsync(ct);

                if (realAssignmentId > 0)
                {
                    assignment = await _context.ExamAssignments
                        .AsNoTracking()
                        .Include(a => a.Exam)
                        .FirstOrDefaultAsync(a => a.Id == realAssignmentId && a.StudentId == studentId && exams.Any(e => e.Id == a.ExamId), ct);
                }
            }

            Exam exam;
            int reportExamId;
            int reportAssignmentId;
            DateTime reportAssignedAt;

            if (assignment != null && assignment.Exam != null)
            {
                exam = assignment.Exam;
                reportExamId = assignment.ExamId;
                reportAssignmentId = assignment.Id;
                reportAssignedAt = assignment.AssignedAt;
            }
            else if (examId > 0)
            {
                // ⚠️ لا يوجد سجل ExamAssignments شخصي لهذا الطالب (مثلاً انضم للدفعة بعد إرسال الاختبار)
                // نبني التقرير مباشرة من إجابات الطالب المحفوظة لنفس الاختبار، بنفس أسلوب AdminPlacementReportPrint
                exam = await exams.AsNoTracking().FirstOrDefaultAsync(e => e.Id == examId, ct);
                if (exam == null)
                    return PlacementReportResult<PlacementReportViewModel>.NotFound(examNotFoundMessage);

                reportExamId = examId;
                reportAssignmentId = 0;
                reportAssignedAt = await _context.ExamStudentStatuses
                    .Where(s => s.StudentId == studentId && s.ExamId == examId)
                    .Select(s => (DateTime?)s.AssignedAt)
                    .FirstOrDefaultAsync(ct) ?? exam.CreatedAt;
            }
            else
            {
                return PlacementReportResult<PlacementReportViewModel>.NotFound(examNotFoundMessage);
            }

            // 🔹 جلب الأسئلة ومحاولات الطالب بنفس بنية تقرير الطالب
            var fullData = await (
                from eq in _context.ExamQuestions.AsNoTracking()
                join q in _context.Questions.AsNoTracking() on eq.QuestionId equals q.Id
                join l in _context.Lessons.AsNoTracking() on q.LessonId equals l.Id
                join s in _context.Sections.AsNoTracking() on l.SectionId equals s.Id
                join c in _context.Curriculums.AsNoTracking() on s.CurriculumId equals c.Id
                join att in _context.QuestionAttemptNew
                    .AsNoTracking()
                    .Where(a => a.StudentId == studentId &&
                                (a.ExamId == reportExamId || a.ExamAssignmentId == reportAssignmentId))
                    on q.Id equals att.QuestionId into gj
                from attempt in gj.DefaultIfEmpty()
                where eq.ExamId == reportExamId
                select new
                {
                    QuestionId = q.Id,
                    q.Title,
                    SectionId = s.Id,
                    SectionName = s.Title,
                    LessonId = l.Id,
                    LessonName = l.Title,
                    CurriculumTitle = c.Title,
                    IsCorrect = attempt != null && attempt.IsCorrect,
                    IsDontKnowAnswer = attempt != null && attempt.IsDontKnowAnswer,
                    HasAttempt = attempt != null
                }
            ).ToListAsync(ct);

            if (!fullData.Any())
                return PlacementReportResult<PlacementReportViewModel>.NoData("⚠️ لا توجد بيانات للأسئلة.");

            // 🔸 حساب النتائج العامة
            int totalQuestions = fullData.Count;
            int correctAnswers = fullData.Count(x => x.IsCorrect);
            int answeredQuestions = fullData.Count(x => x.HasAttempt);
            int skippedQuestions = totalQuestions - answeredQuestions;
            int wrongAnswers = fullData.Count(x => x.HasAttempt && !x.IsCorrect);
            // 🆕 من ضمن wrongAnswers: عدد إجابات "لا أعرف" تحديدًا
            int dontKnowAnswers = fullData.Count(x => x.IsDontKnowAnswer);

            double overall = totalQuestions > 0
                ? Math.Round(correctAnswers * 100.0 / totalQuestions, 1)
                : 0.0;

            // 🕒 حساب وقت الحل
            var status = await _context.ExamStudentStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentId == studentId &&
                                          (s.ExamId == reportExamId || s.ExamAssignmentId == reportAssignmentId), ct);

            int solveMinutes = 0;
            if (status != null && status.StartedAt.HasValue && status.SubmittedAt.HasValue)
                solveMinutes = (int)Math.Round((status.SubmittedAt.Value - status.StartedAt.Value).TotalMinutes);
            else
            {
                // 🟡 في حالة عدم وجود بيانات نستخدم الوقت الإجمالي الافتراضي
                var totalSeconds = await _context.QuestionAttemptNew
                    .Where(a => a.StudentId == studentId &&
                                (a.ExamId == reportExamId || a.ExamAssignmentId == reportAssignmentId))
                    .SumAsync(a => (int?)a.TimeTakenSeconds, ct) ?? 0;

                solveMinutes = totalSeconds > 0 ? (int)Math.Round(totalSeconds / 60.0) : exam.DurationMinutes;
            }

            // 🧮 زمن الاختبار الكلي
            double totalMinutes = exam.DurationMinutes > 0
                ? exam.DurationMinutes
                : 60;

            // 🔹 نسبة الوقت المستهلك
            double percentTime = totalMinutes > 0
                ? Math.Round((solveMinutes / totalMinutes) * 100, 1)
                : 0.0;

            // 🔹 التوصية الذكية
            var recommendation = _recommendationService.GetRecommendation(overall, solveMinutes, totalMinutes);

            // 🔹 تحليل المحاور بنفس منطق تقرير الطالب: النسبة من الأسئلة المجابة داخل المحور
            var groupedSections = fullData
                .GroupBy(x => new { x.SectionName, x.CurriculumTitle })
                .Select(g => new
                {
                    g.Key.SectionName,
                    g.Key.CurriculumTitle,
                    TotalQuestions = g.Count(),
                    CorrectCount = g.Count(x => x.IsCorrect),
                    WrongCount = g.Count(x => x.HasAttempt && !x.IsCorrect),
                    Percent = g.Any(x => x.HasAttempt)
                        ? Math.Round(g.Count(x => x.IsCorrect) * 100.0 / g.Count(x => x.HasAttempt), 1)
                        : 0.0
                })
                .ToList();

            // 🔹 تحليل الكمي
            var quantGroups = groupedSections
                .Where(x => x.CurriculumTitle.IndexOf("كمي", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            // 🔹 تحليل اللفظي
            var verbalGroups = groupedSections
                .Where(x => x.CurriculumTitle.IndexOf("لفظي", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            // ✅ بناء النموذج
            var model = new PlacementReportViewModel
            {
                Student = new StudentMiniVm
                {
                    FullName = student.FullName,
                    StudentID = student.StudentID,
                    Level = student.Level,
                    ParentName = student.Parent?.FullName ?? "—",
                    ParentPhone = student.Parent?.PhoneNumber ?? "—"
                },
                ExamDate = reportAssignedAt.ToString("yyyy-MM-dd"),
                SolveMinutes = solveMinutes,
                OverallPercent = overall,
                TotalMinutes = totalMinutes,
                PercentTime = percentTime,
                AssignmentId = reportAssignmentId,
                WrongAnswers = wrongAnswers,
                Recommendation = recommendation,

                QuantLabels = quantGroups.Select(x => x.SectionName).ToList(),
                QuantScores = quantGroups.Select(x => x.Percent).ToList(),
                QuantCorrectCounts = quantGroups.Select(x => x.CorrectCount).ToList(),
                QuantWrongCounts = quantGroups.Select(x => x.WrongCount).ToList(),

                VerbalLabels = verbalGroups.Select(x => x.SectionName).ToList(),
                VerbalScores = verbalGroups.Select(x => x.Percent).ToList(),
                VerbalCorrectCounts = verbalGroups.Select(x => x.CorrectCount).ToList(),
                VerbalWrongCounts = verbalGroups.Select(x => x.WrongCount).ToList(),

                HasQuantChart = quantGroups.Any(),
                HasVerbalChart = verbalGroups.Any(),

                TotalQuestions = totalQuestions,
                CorrectAnswers = correctAnswers,
                AnsweredQuestions = answeredQuestions,
                SkippedQuestions = skippedQuestions,
                DontKnowAnswers = dontKnowAnswers
            };

            // ✅ تحليل المؤشرات داخل كل محور
            model.SectionLessonReports = fullData
                .GroupBy(x => new { x.SectionId, x.SectionName })
                .Select(g => new QdratNew.ViewModels.Exam.SectionLessonReportVm
                {
                    SectionId = g.Key.SectionId,
                    SectionName = g.Key.SectionName,
                    Lessons = g.GroupBy(l => new { l.LessonId, l.LessonName })
                        .Select(lessonGroup => new QdratNew.ViewModels.Exam.LessonPerformanceVm
                        {
                            LessonId = lessonGroup.Key.LessonId,
                            LessonName = lessonGroup.Key.LessonName,
                            TotalQuestions = lessonGroup.Count(),
                            CorrectCount = lessonGroup.Count(x => x.IsCorrect),
                            WrongCount = lessonGroup.Count(x => x.HasAttempt && !x.IsCorrect),
                            SkippedCount = lessonGroup.Count(x => !x.HasAttempt),
                            Percent = lessonGroup.Any(x => x.HasAttempt)
                                ? Math.Round(lessonGroup.Count(x => x.IsCorrect) * 100.0 / lessonGroup.Count(x => x.HasAttempt), 1)
                                : 0.0
                        }).OrderBy(x => x.LessonName).ToList()
                })
                .OrderBy(x => x.SectionName)
                .ToList();

            return PlacementReportResult<PlacementReportViewModel>.Ok(model);
        }

        // =====================================================
        // AdminPlacementReportPrint
        // =====================================================
        public async Task<PlacementReportResult<AdminPlacementReportViewModel>> GetStudentPlacementReportPrintAsync(PlacementScope scope, int examId, int studentId, CancellationToken ct = default)
        {
            var student = await _guard.StudentsWithResults(_context, scope)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentID == studentId, ct);
            var exam = await _guard.ExamsInScope(_context, scope)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == examId, ct);

            if (student == null || exam == null)
                return PlacementReportResult<AdminPlacementReportViewModel>.NotFound();

            // ✅ جلب BatchId من جدول ExamAssignmentsToBatches
            var batchId = await StudentBatchLinks(scope, studentId)
                .Where(e => e.ExamId == examId)
                .Select(e => e.BatchId)
                .FirstOrDefaultAsync(ct);

            // ✅ جلب محاولات الطالب لهذا الاختبار
            var attempts = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                where a.StudentId == studentId && a.ExamId == examId
                select new
                {
                    a.IsCorrect,
                    a.IsDontKnowAnswer,
                    a.TimeTakenSeconds,
                    SectionTitle = s.Title,
                    Type = q.IsQuantitative ? "Quantitative" : "Verbal"
                }
            ).ToListAsync(ct);

            if (!attempts.Any())
                return PlacementReportResult<AdminPlacementReportViewModel>.NoData("لا توجد بيانات لهذا الطالب في هذا الاختبار.", batchId);

            // ✅ الحسابات العامة
            int total = attempts.Count;
            int correct = attempts.Count(x => x.IsCorrect);
            // 🆕 عدد إجابات "لا أعرف الإجابة"
            int dontKnow = attempts.Count(x => x.IsDontKnowAnswer);
            double percent = Math.Round((double)correct * 100 / total, 2);

            // ✅ حساب الزمن الفعلي من ExamStudentStatuses
            var status = await _context.ExamStudentStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentId == studentId && s.ExamId == examId, ct);

            double avgTime = 0;
            if (status != null && status.StartedAt.HasValue && status.SubmittedAt.HasValue)
            {
                avgTime = Math.Round((status.SubmittedAt.Value - status.StartedAt.Value).TotalMinutes, 1);
            }

            // ✅ تحليل المحاور الكمية
            var quantData = attempts
                .Where(x => x.Type == "Quantitative")
                .GroupBy(x => x.SectionTitle)
                .Select(g => new
                {
                    Label = g.Key,
                    Score = Math.Round(g.Count(x => x.IsCorrect) * 100.0 / g.Count(), 2)
                }).ToList();

            // ✅ تحليل المحاور اللفظية
            var verbalData = attempts
                .Where(x => x.Type == "Verbal")
                .GroupBy(x => x.SectionTitle)
                .Select(g => new
                {
                    Label = g.Key,
                    Score = Math.Round(g.Count(x => x.IsCorrect) * 100.0 / g.Count(), 2)
                }).ToList();

            // ✅ تحديد التوصيات
            string recommendation;
            string adminDecision;

            if (percent < 50)
            {
                recommendation = "الطالب يعاني من ضعف واضح في أغلب المحاور، ويحتاج إلى خطة علاجية مركزة.";
                adminDecision = "يوصى بتفعيل جلسات دعم فردية ومتابعة أسبوعية من قبل المدرب.";
            }
            else if (percent < 70)
            {
                recommendation = "الطالب بمستوى متوسط ويحتاج إلى مراجعة المحاور الضعيفة وتعزيز الجانب الكمي أو اللفظي.";
                adminDecision = "يُفضل إعادة اختبار تقييم بعد أسبوعين لتحديد التحسن.";
            }
            else
            {
                recommendation = "الطالب يُظهر أداءً قويًا وثابتًا، ومستعد للترقية إلى المستوى التالي.";
                adminDecision = "اعتماد الترقية الأكاديمية وتحديث الخطة الدراسية بناءً على نتائجه.";
            }

            // ✅ إنشاء ViewModel
            return PlacementReportResult<AdminPlacementReportViewModel>.Ok(new AdminPlacementReportViewModel
            {
                ExamTitle = exam.Title,
                // PPL-S6: صفحة الشريك تعرض اسم الطالب؛ موديل الأدمن بلا تغيير
                StudentName = scope.IsPartner ? student.FullName : null,
                StudentId = scope.IsPartner ? student.StudentID : 0,
                ExamDate = exam.CreatedAt,
                OverallPercent = percent,
                AverageSolveMinutes = avgTime,
                QuantLabels = quantData.Select(x => x.Label).ToList(),
                QuantScores = quantData.Select(x => x.Score).ToList(),
                VerbalLabels = verbalData.Select(x => x.Label).ToList(),
                VerbalScores = verbalData.Select(x => x.Score).ToList(),
                RecommendationText = recommendation,
                AdminDecisionTip = adminDecision,
                DontKnowAnswers = dontKnow,
            });
        }

        // =====================================================
        // DownloadStudentReport (QuestPDF)
        // =====================================================
        public async Task<(byte[] Pdf, string FileName)?> BuildStudentReportPdfAsync(PlacementScope scope, int examId, int studentId, CancellationToken ct = default)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var student = await _guard.StudentsWithResults(_context, scope)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentID == studentId, ct);
            var exam = await _guard.ExamsInScope(_context, scope)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == examId, ct);

            if (student == null || exam == null)
                return null;

            var answers = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                where a.StudentId == studentId && a.ExamId == examId
                select new
                {
                    q.Title,
                    q.CorrectAnswer,
                    a.SelectedAnswer,
                    a.IsCorrect
                }
            ).ToListAsync(ct);

            var correct = answers.Count(x => x.IsCorrect);
            var wrong = answers.Count(x => !x.IsCorrect);

            // PPL-S6: ترويسة الشريك؛ نطاق الأدمن بلا تغيير
            var branding = await GetReportBrandingAsync(scope, ct);
            var partnerLogo = branding == null ? null : await LoadPartnerLogoAsync(scope, ct);

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontFamily("Cairo").FontSize(13));

                    // 🟦 رأس الصفحة مع اللوجو والعنوان
                    page.Header().Row(row =>
                    {
                        if (branding != null)
                        {
                            row.RelativeColumn().Column(title =>
                            {
                                title.Item().AlignCenter().Text(branding.PartnerName).Bold().FontSize(18).FontColor("#9a4a26");
                                title.Item().AlignCenter().Text(PartnerReportCollaborationText).FontSize(11).FontColor("#666");
                                title.Item().AlignCenter().PaddingTop(4).Text("تقرير اختبار الطالب").Bold().FontSize(14);
                            });

                            if (partnerLogo != null)
                                row.ConstantColumn(80).Image(partnerLogo);

                            return;
                        }

                        row.RelativeColumn()
                            .AlignCenter()
                            .Text("تقرير اختبار الطالب")
                            .Bold()
                            .FontSize(18)
                            .FontColor("#0078D7");

                        var logoPath = Path.Combine("wwwroot", "images", "160×100.png");
                        if (File.Exists(logoPath))
                            row.ConstantColumn(80).Image(logoPath);
                    });

                    // 📋 بيانات الطالب بصندوق ملون لطيف
                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        col.Item().Background("#f4f8ff").Padding(15).Column(info =>
                        {
                            info.Item().AlignRight().Text($"👨‍🎓 الطالب: {student.FullName}").Bold();
                            info.Item().AlignRight().Text($"🧠 الاختبار: {exam.Title}");
                            info.Item().AlignRight().Text($"📅 تاريخ الإنشاء: {exam.CreatedAt:yyyy-MM-dd HH:mm}");
                            info.Item().AlignRight().Text($"✅ الصحيحة: {correct}    ❌ الخاطئة: {wrong}");
                        });

                        col.Item().PaddingVertical(10);

                        // 🧾 جدول الأسئلة
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40); // م
                                columns.RelativeColumn(3);   // الإجابة الصحيحة
                                columns.RelativeColumn(3);   // إجابتك
                                columns.RelativeColumn(6);   // السؤال
                            });

                            // رأس الجدول
                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).AlignCenter().Text("م").Bold();
                                header.Cell().Element(CellStyle).AlignCenter().Text("الإجابة الصحيحة").Bold();
                                header.Cell().Element(CellStyle).AlignCenter().Text(branding != null ? "إجابة الطالب" : "إجابتك").Bold();
                                header.Cell().Element(CellStyle).AlignCenter().Text("السؤال").Bold();
                            });

                            // نصوص بنك الأسئلة قد تحتوي HTML، وPDF الشريك يعرضها كنص (الأدمن كما كان)
                            string PdfText(string? value) =>
                                branding != null ? PlacementReportText.ToPlainText(value) : value ?? "—";

                            int index = 1;
                            foreach (var a in answers)
                            {
                                table.Cell().Element(CellStyle).AlignCenter().Text(index++.ToString());
                                table.Cell().Element(CellStyle).AlignCenter()
                                    .Text(PdfText(a.CorrectAnswer)).FontColor("#0B6E4F");
                                table.Cell().Element(CellStyle).AlignCenter()
                                    .Text(PdfText(a.SelectedAnswer))
                                    .FontColor(a.IsCorrect ? "#198754" : "#C00");
                                table.Cell().Element(CellStyle).AlignRight()
                                    .Text(PdfText(a.Title));
                            }

                            // 🧱 تصميم الخلايا
                            static IContainer CellStyle(IContainer container)
                            {
                                return container.Border(0.5f)
                                                .BorderColor("#D0D7E2")
                                                .Padding(5)
                                                .AlignMiddle();
                            }
                        });
                    });

                    // 🕓 التذييل
                    page.Footer().AlignCenter()
                        .Text($"تم إنشاء التقرير بتاريخ {_timeZoneService.GetNowSaudi():yyyy/MM/dd HH:mm}")
                        .FontSize(10)
                        .FontColor("#666");
                });
            });

            return (pdf.GeneratePdf(), $"تقرير_{student.FullName}.pdf");
        }

        private async Task<QuestPDF.Infrastructure.Image?> LoadPartnerLogoAsync(PlacementScope scope, CancellationToken ct)
        {
            var partnerId = scope.PartnerId!.Value;
            var logoPath = await _context.Partners
                .AsNoTracking()
                .Where(p => p.Id == partnerId)
                .Select(p => p.LogoPath)
                .FirstOrDefaultAsync(ct);

            var filePath = GetPartnerLogoFilePath(logoPath);
            if (filePath == null)
                return null;

            try
            {
                return QuestPDF.Infrastructure.Image.FromBinaryData(await File.ReadAllBytesAsync(filePath, ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // صيغة لا يدعمها QuestPDF (مثل svg): التقرير يصدر بالاسم فقط
                _logger.LogWarning(ex, "PPL-S6: تعذر تحميل شعار الشريك {PartnerId}", partnerId);
                return null;
            }
        }

        // =====================================================
        // DownloadReportChrome (PuppeteerSharp)
        // =====================================================
        public async Task<(byte[] Pdf, string FileName)?> BuildStudentReportChromePdfAsync(PlacementScope scope, int examId, int studentId, string reportUrl, CancellationToken ct = default)
        {
            if (await _guard.IsExamDeniedAsync(_context, scope, examId, ct) ||
                !await _guard.CanAccessStudentResultsAsync(_context, scope, studentId, ct))
                return null;

            // 🔹 تحميل Chrome في أول مرة فقط
            var executablePath = await _reportBrowserProvider.GetExecutablePathAsync(ct);

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = executablePath,
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
            });

            var page = await browser.NewPageAsync();
            await page.GoToAsync(reportUrl, WaitUntilNavigation.Networkidle2);

            // 🔹 إنشاء PDF بجودة عالية واتجاه RTL سليم
            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions
                {
                    Top = "10mm",
                    Bottom = "10mm",
                    Left = "10mm",
                    Right = "10mm"
                }
            });

            return (pdfBytes, $"تقرير_{studentId}.pdf");
        }
    }

    // PPL-S6
    public static class PlacementReportText
    {
        private static readonly Regex HtmlTags = new("<[^>]*>", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        /// <summary>
        /// نص بلا وسوم لعرضه مُرمَّزًا. إجابة الطالب يرسلها المتصفح نصًا حرًا، فلا تُعرض أبدًا بـ Html.Raw.
        /// </summary>
        public static string ToPlainText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "—";

            var text = Spaces.Replace(WebUtility.HtmlDecode(HtmlTags.Replace(value, " ")), " ").Trim();

            return text.Length == 0 ? "—" : text;
        }
    }
}
