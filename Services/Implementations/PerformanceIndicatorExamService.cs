using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.Implementations
{
    /// <summary>
    /// ✅ خدمة توليد اختبارات مؤشر الأداء (Performance Indicator Exam)
    /// - متوافقة مع SQL Server 2014
    /// - تمنع التكرار بين الدفعات
    /// - تدعم الحذف القسري الآمن حتى لو الطلاب اختبروا
    /// - تستخدم IDbContextFactory + MemoryCache لتحسين الأداء
    /// </summary>
    public class PerformanceIndicatorExamService : IPerformanceIndicatorExamService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IMemoryCache _cache;

        public PerformanceIndicatorExamService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IMemoryCache cache)
        {
            _contextFactory = contextFactory;
            _cache = cache;
        }

        // 🟢 توليد الاختبار بناءً على المنهج والدفعة المحددة
        public async Task GenerateExamAsync(int examId, int curriculumId, int totalQuestions, bool manualSelection, int? professionalModelId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var exam = await _context.PerformanceIndicatorExams
                .Include(e => e.Sections)
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null)
                throw new Exception("❌ لم يتم العثور على بيانات الاختبار.");

            // ✅ جلب الدفعات المرتبطة بهذا الاختبار فقط
            var linkedBatches = await _context.PerformanceIndicatorExamToBatch
                .Where(x => x.PerformanceIndicatorExamId == exam.Id)
                .Select(x => x.BatchId)
                .ToListAsync();

            if (!linkedBatches.Any())
                throw new Exception("⚠️ لا توجد دفعات مرتبطة بهذا الاختبار.");

            // 🟢 جلب المحاور التابعة للمنهج
            var sections = await _context.Sections
                .AsNoTracking()
                .Where(s => s.CurriculumId == curriculumId)
                .ToListAsync();

            if (!sections.Any())
                throw new Exception("⚠️ لا توجد محاور مرتبطة بهذا المنهج.");

            int totalGenerated = 0;
            int totalPerSection = totalQuestions;

            // 🟩 إنشاء الأسئلة حسب كل محور
            foreach (var section in sections)
            {
                var examSection = new PerformanceIndicatorExamSection
                {
                    PerformanceIndicatorExamId = exam.Id,
                    SectionId = section.Id,
                    QuestionCount = 0
                };
                _context.PerformanceIndicatorExamSections.Add(examSection);

                var lessons = await _context.Lessons
                    .AsNoTracking()
                    .Where(l => l.SectionId == section.Id && l.IsActive)
                    .ToListAsync();

                List<Guid> selectedQuestionIds = new();

                // 🟡 سؤال واحد من كل درس مفعّل
                foreach (var lesson in lessons)
                {
                    var oneQuestion = await _context.Questions
                        .AsNoTracking()
                        .Where(q => q.LessonId == lesson.Id &&
                                    q.IsReviewed &&
                                    q.IsComplete &&
                                    !q.IsRejected &&
                                    q.CorrectAnswer != null)
                        .OrderBy(r => Guid.NewGuid())
                        .Select(q => q.Id)
                        .FirstOrDefaultAsync();

                    if (oneQuestion != Guid.Empty)
                        selectedQuestionIds.Add(oneQuestion);
                }

                // 🔹 كمل الباقي عشوائيًا من نفس المحور
                int remaining = totalPerSection - selectedQuestionIds.Count;
                if (remaining > 0)
                {
                    var extraQuestions = await _context.Questions
                        .AsNoTracking()
                        .Include(q => q.Lesson)
                        .Where(q => q.SectionId == section.Id &&
                                    q.IsReviewed &&
                                    q.IsComplete &&
                                    !q.IsRejected &&
                                    q.CorrectAnswer != null &&
                                    q.Lesson.IsActive &&
                                    !_context.PerformanceIndicatorExamQuestions
                                        .Any(eq => eq.PerformanceIndicatorExamId == exam.Id && eq.QuestionId == q.Id))
                        .OrderBy(r => Guid.NewGuid())
                        .Take(remaining)
                        .Select(q => q.Id)
                        .ToListAsync();

                    selectedQuestionIds.AddRange(extraQuestions);
                }

                selectedQuestionIds = selectedQuestionIds.Take(totalPerSection).ToList();

                int order = 1;
                foreach (var qid in selectedQuestionIds.Distinct())
                {
                    _context.PerformanceIndicatorExamQuestions.Add(new PerformanceIndicatorExamQuestion
                    {
                        PerformanceIndicatorExamId = exam.Id,
                        SectionId = section.Id,
                        QuestionId = qid,
                        OrderNumber = order++
                    });
                    totalGenerated++;
                }

                examSection.QuestionCount = selectedQuestionIds.Count;
            }

            // 🕒 زمن الاختبار = عدد الأسئلة
            exam.DurationMinutes = totalGenerated > 0 ? totalGenerated : 1;

            // حفظ العدد الإجمالي في الكيان الرئيسي (لو الخاصية موجودة)
            if (_context.Entry(exam).Property("TotalQuestions") != null)
                exam.GetType().GetProperty("TotalQuestions")?.SetValue(exam, totalGenerated);

            await _context.SaveChangesAsync();

            // ✅ بعد توليد الأسئلة: ربط الطلاب بالاختبار حسب الدفعات فقط
            // ✅ جلب الطلاب المنتمين لكل دفعة من جدول StudentBatchEnrollments
            foreach (var batchId in linkedBatches)
            {
                var studentsInBatch = await (
                    from e in _context.StudentBatchEnrollments
                    join s in _context.Students on e.StudentID equals s.StudentID
                    where e.BatchId == batchId
                    select s.StudentID
                ).ToListAsync();

                foreach (var studentId in studentsInBatch)
                {
                    bool alreadyLinked = await _context.PerformanceIndicatorExamStudents
                        .AnyAsync(x => x.PerformanceIndicatorExamId == exam.Id && x.StudentId == studentId);

                    if (!alreadyLinked)
                    {
                        _context.PerformanceIndicatorExamStudents.Add(new PerformanceIndicatorExamStudent
                        {
                            PerformanceIndicatorExamId = exam.Id,
                            StudentId = studentId,
                            IsCompleted = false,
                            StartedAt = null,
                            ScorePercent = null,
                            CompletedAt = null
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();


            await _context.SaveChangesAsync();
        }

        // 🟢 جلب كل اختبارات دفعة معينة
        public async Task<List<PerformanceIndicatorExam>> GetExamsByBatchAsync(int batchId)
        {
            using var _context = _contextFactory.CreateDbContext();

            return await _cache.GetOrCreateAsync($"Exams_Batch_{batchId}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

                return await (
                    from e in _context.PerformanceIndicatorExams.AsNoTracking()
                    join eb in _context.PerformanceIndicatorExamToBatch.AsNoTracking()
                        on e.Id equals eb.PerformanceIndicatorExamId
                    where eb.BatchId == batchId
                    select e
                )
                .Include(e => e.Curriculum)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
            });
        }

        // 🟢 جلب اختبار كامل بالتفاصيل
        public async Task<PerformanceIndicatorExam?> GetExamWithSectionsAsync(int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            return await _context.PerformanceIndicatorExams
                .AsNoTracking()
                .Include(e => e.Sections)
                .ThenInclude(s => s.Section)
                .FirstOrDefaultAsync(e => e.Id == examId);
        }

        // 🟢 حذف الاختبار نهائيًا مع جميع علاقاته
        // 🟢 حذف الاختبار نهائيًا مع جميع علاقاته
        // 🟢 حذف الاختبار نهائيًا مع جميع علاقاته
        public async Task<bool> DeleteExamAsync(int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var exam = await _context.PerformanceIndicatorExams
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null)
                return false;

            // 🧹 حذف العلاقات التابعة يدويًا (بترتيب آمن)
            var attempts = await _context.QuestionAttemptNew
                .Where(a => a.PerformanceIndicatorExamId == examId)
                .ToListAsync();
            _context.QuestionAttemptNew.RemoveRange(attempts);

            var questions = await _context.PerformanceIndicatorExamQuestions
                .Where(q => q.PerformanceIndicatorExamId == examId)
                .ToListAsync();
            _context.PerformanceIndicatorExamQuestions.RemoveRange(questions);

            var sections = await _context.PerformanceIndicatorExamSections
                .Where(s => s.PerformanceIndicatorExamId == examId)
                .ToListAsync();
            _context.PerformanceIndicatorExamSections.RemoveRange(sections);

            var students = await _context.PerformanceIndicatorExamStudents
                .Where(s => s.PerformanceIndicatorExamId == examId)
                .ToListAsync();
            _context.PerformanceIndicatorExamStudents.RemoveRange(students);

            var batches = await _context.PerformanceIndicatorExamToBatch
                .Where(b => b.PerformanceIndicatorExamId == examId)
                .ToListAsync();
            _context.PerformanceIndicatorExamToBatch.RemoveRange(batches);

            var results = await _context.StudentIndicatorResults
                .Where(r => r.PerformanceIndicatorExamId == examId)
                .ToListAsync();
            _context.StudentIndicatorResults.RemoveRange(results);

            // 🔥 حذف الكيان الرئيسي بعد إزالة كل ما يعتمد عليه
            _context.PerformanceIndicatorExams.Remove(exam);
            await _context.SaveChangesAsync();

            // 🧼 تنظيف الكاش
            _cache.Remove($"Exams_Batch_{exam.BatchId}");

            return true;
        }


        // 🟢 تسجيل نتيجة الطالب
        public async Task RecordStudentResultAsync(int studentId, int examId, int sectionId, double scorePercent, double avgTime, bool rushed, bool unfocused)
        {
            using var _context = _contextFactory.CreateDbContext();

            var result = new StudentIndicatorResult
            {
                StudentId = studentId,
                PerformanceIndicatorExamId = examId,
                SectionId = sectionId,
                ScorePercent = scorePercent,
                AverageTimePerQuestion = avgTime,
                WasRushed = rushed,
                WasUnfocused = unfocused,
                TakenAt = DateTime.Now
            };

            await _context.StudentIndicatorResults.AddAsync(result);
            await _context.SaveChangesAsync();
        }
    }
}
