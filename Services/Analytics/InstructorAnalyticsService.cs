using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Admin.Analytics;

namespace QdratNew.Services.Analytics
{
    public class InstructorAnalyticsService : IInstructorAnalyticsService
    {
        private readonly ApplicationDbContext _context;

        public InstructorAnalyticsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<InstructorPerformanceDetailsVM?> BuildInstructorPerformanceAsync(int instructorId)
        {
            var instructor = await _context.Set<Instructor>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == instructorId);

            if (instructor == null) return null;

            // ── اجمع batch IDs الخاصة بهذا المدرب في SQL ────────────────────
            var batchIdsFromICB = await _context.Set<InstructorCurriculumBatch>()
                .AsNoTracking()
                .Where(x => x.InstructorId == instructorId)
                .Select(x => x.BatchId)
                .Distinct()
                .ToListAsync();

            var courseIds = await _context.Set<CourseInstructor>()
                .AsNoTracking()
                .Where(x => x.InstructorID == instructorId)
                .Select(x => x.CourseID)
                .Distinct()
                .ToListAsync();

            var batchIdsFromCourses = courseIds.Count > 0
                ? await _context.Set<Batch>()
                    .AsNoTracking()
                    .Where(b => courseIds.Contains(b.CourseId))
                    .Select(b => b.Id)
                    .ToListAsync()
                : new List<int>();

            var batchIdsFromAssignments = await _context.Set<ExamAssignmentToBatch>()
                .AsNoTracking()
                .Where(x => x.CreatedByInstructorId == instructorId)
                .Select(x => x.BatchId)
                .Distinct()
                .ToListAsync();

            var allBatchIds = batchIdsFromICB
                .Union(batchIdsFromCourses)
                .Union(batchIdsFromAssignments)
                .Distinct()
                .ToList();

            if (!allBatchIds.Any())
                return BuildEmptyResult(instructor);

            // ── تحميل محاولات المدرب فقط عبر ExamAssignmentToBatch ───────────
            var assignmentIds = await _context.Set<ExamAssignmentToBatch>()
                .AsNoTracking()
                .Where(x => x.CreatedByInstructorId == instructorId)
                .Select(x => x.Id)
                .ToListAsync();

            var attempts = assignmentIds.Count > 0
                ? await _context.QuestionAttemptNew
                    .AsNoTracking()
                    .Where(x => x.ExamAssignmentId.HasValue && assignmentIds.Contains(x.ExamAssignmentId.Value))
                    .ToListAsync()
                : new List<QuestionAttemptNew>();

            // ── البيانات المساعدة ─────────────────────────────────────────────
            var batchesDb = await _context.Set<Batch>()
                .AsNoTracking()
                .Where(b => allBatchIds.Contains(b.Id))
                .ToListAsync();

            var courseIdsList = batchesDb.Select(b => b.CourseId).Distinct().ToList();
            var coursesDb = await _context.Set<Course>()
                .AsNoTracking()
                .Where(c => courseIdsList.Contains(c.Id))
                .ToListAsync();

            var lessonsDb = await _context.Set<Lesson>().AsNoTracking().ToListAsync();
            var questionsDb = await _context.Set<Question>().AsNoTracking().ToListAsync();
            var enrollments = await _context.Set<StudentBatchEnrollment>()
                .AsNoTracking()
                .Where(e => allBatchIds.Contains(e.BatchId))
                .ToListAsync();

            var allExamAssignments = await _context.Set<ExamAssignmentToBatch>()
                .AsNoTracking()
                .Where(x => x.CreatedByInstructorId == instructorId)
                .ToListAsync();

            // ── فهارس O(1) ───────────────────────────────────────────────────
            var batchMap = batchesDb.ToDictionary(b => b.Id);
            var courseMap = coursesDb.ToDictionary(c => c.Id);
            var lessonMap = lessonsDb.ToDictionary(l => l.Id);
            var questionMap = questionsDb.ToDictionary(q => q.Id);

            var enrollmentsByBatch = enrollments
                .GroupBy(e => e.BatchId)
                .ToDictionary(g => g.Key, g => new HashSet<int>(g.Select(e => e.StudentID)));

            var assignmentsByBatch = allExamAssignments
                .GroupBy(a => a.BatchId)
                .ToDictionary(g => g.Key, g => new HashSet<int>(g.Select(a => a.Id)));

            // ── تحليل الدفعات ─────────────────────────────────────────────────
            var batchGroups = allBatchIds
                .Select(currentBatchId =>
                {
                    batchMap.TryGetValue(currentBatchId, out var batch);
                    var courseId = batch?.CourseId ?? 0;
                    courseMap.TryGetValue(courseId, out var course);

                    var batchAssignmentIds = assignmentsByBatch.GetValueOrDefault(currentBatchId)
                        ?? new HashSet<int>();

                    var batchAttempts = attempts
                        .Where(x => x.ExamAssignmentId.HasValue && batchAssignmentIds.Contains(x.ExamAssignmentId.Value))
                        .ToList();

                    var total = batchAttempts.Count;
                    var correct = batchAttempts.Count(x => x.IsCorrect);
                    var wrong = batchAttempts.Count(x => !x.IsCorrect);

                    var avgScore = total == 0 ? 0 : (correct * 100.0) / total;
                    var weaknessPct = total == 0 ? 0 : (wrong * 100.0) / total;

                    var studentsCount = enrollmentsByBatch.GetValueOrDefault(currentBatchId)?.Count ?? 0;

                    var weakLessons = batchAttempts
                        .Where(x => x.LessonId.HasValue)
                        .GroupBy(x => x.LessonId!.Value)
                        .Select(lg =>
                        {
                            var lt = lg.Count();
                            var lw = lg.Count(x => !x.IsCorrect);
                            var lessonWeakness = lt == 0 ? 0 : (lw * 100.0) / lt;

                            lessonMap.TryGetValue(lg.Key, out var lesson);

                            var highRiskQuestions = lg
                                .GroupBy(x => x.QuestionId)
                                .Select(qg =>
                                {
                                    var qt = qg.Count();
                                    var qw = qg.Count(x => !x.IsCorrect);
                                    var errPct = qt == 0 ? 0 : (qw * 100.0) / qt;

                                    questionMap.TryGetValue(qg.Key, out var question);

                                    return new InstructorHighRiskQuestionVM
                                    {
                                        QuestionId = qg.Key,
                                        ReferenceNumber = question?.ReferenceNumber ?? "",
                                        QuestionTitle = question?.Title ?? "سؤال غير معروف",
                                        AttemptsCount = qt,
                                        WrongCount = qw,
                                        ErrorPercentage = Math.Round(errPct, 1)
                                    };
                                })
                                .Where(x => x.ErrorPercentage is >= 60 and <= 100)
                                .OrderByDescending(x => x.ErrorPercentage)
                                .ToList();

                            return new InstructorWeakLessonVM
                            {
                                LessonId = lg.Key,
                                LessonName = lesson?.Title ?? "مؤشر غير معروف",
                                AffectedStudents = lg.Where(x => !x.IsCorrect).Select(x => x.StudentId).Distinct().Count(),
                                WeaknessPercentage = Math.Round(lessonWeakness, 1),
                                HighRiskQuestions = highRiskQuestions
                            };
                        })
                        .Where(x => x.WeaknessPercentage >= 60 && x.HighRiskQuestions.Any())
                        .OrderByDescending(x => x.WeaknessPercentage)
                        .ToList();

                    return new InstructorBatchPerformanceVM
                    {
                        BatchId = currentBatchId,
                        BatchName = batch?.Name ?? "-",
                        CourseName = course?.Name ?? "-",
                        StudentsCount = studentsCount,
                        AvgScore = Math.Round(avgScore, 1),
                        WeaknessPercentage = Math.Round(weaknessPct, 1),
                        WeakLessons = weakLessons
                    };
                })
                .OrderByDescending(x => x.WeaknessPercentage)
                .ToList();

            // ── التقييم الإجمالي ──────────────────────────────────────────────
            var totalAttempts = attempts.Count;
            var totalCorrect = attempts.Count(x => x.IsCorrect);
            var overallScore = totalAttempts == 0 ? 0 : (totalCorrect * 100.0) / totalAttempts;

            var (scientificRating, ratingDesc, riskLevel, recommendation) = ClassifyScore(overallScore);

            return new InstructorPerformanceDetailsVM
            {
                InstructorId = instructor.Id,
                InstructorName = instructor.FullName,
                OverallScore = Math.Round(overallScore, 1),
                ScientificRating = scientificRating,
                RatingDescription = ratingDesc,
                RiskLevel = riskLevel,
                DecisionRecommendation = recommendation,
                TotalBatches = batchGroups.Count,
                TotalWeakLessons = batchGroups.Sum(x => x.WeakLessons.Count),
                TotalHighRiskQuestions = batchGroups.Sum(x => x.WeakLessons.Sum(l => l.HighRiskQuestions.Count)),
                Batches = batchGroups
            };
        }

        private static (string rating, string desc, string risk, string rec) ClassifyScore(double score)
        {
            if (score >= 90) return ("A+", "أداء تدريبي ممتاز جدًا وفق مؤشرات Mastery Learning.", "منخفض",
                "الحفاظ على نفس أسلوب الشرح مع مشاركة أفضل الممارسات مع باقي المدربين.");
            if (score >= 80) return ("A", "أداء قوي، مع وجود مؤشرات محدودة تحتاج مراجعة.", "منخفض",
                "مراجعة الأسئلة الأعلى خطأ داخل الدفعات لتحسين الاتقان.");
            if (score >= 70) return ("B", "أداء مقبول تربويًا، لكن يحتاج تدخل تحسين واضح في المؤشرات الضعيفة.", "متوسط",
                "إعادة شرح المؤشرات التي تجاوزت 60% أخطاء مع اختبار قصير بعدها.");
            if (score >= 60) return ("C", "أداء يحتاج متابعة؛ نسبة الأخطاء تشير إلى ضعف في نقل المهارة.", "مرتفع",
                "توجيه المدرب لإعادة شرح الأسئلة عالية الخطأ ومراجعة طريقة التدريب.");
            return ("D", "أداء خطر تعليميًا؛ يحتاج تدخل إداري وتدريبي مباشر.", "عالي جدًا",
                "جلسة مراجعة مع المدرب وخطة تحسين إلزامية للدفعات المتأثرة.");
        }

        private static InstructorPerformanceDetailsVM BuildEmptyResult(Instructor instructor)
        {
            return new InstructorPerformanceDetailsVM
            {
                InstructorId = instructor.Id,
                InstructorName = instructor.FullName,
                OverallScore = 0,
                ScientificRating = "-",
                RatingDescription = "لا توجد بيانات كافية لتقييم هذا المدرب.",
                RiskLevel = "غير محدد",
                DecisionRecommendation = "يرجى ربط المدرب بدفعات نشطة.",
                TotalBatches = 0
            };
        }
    }
}
