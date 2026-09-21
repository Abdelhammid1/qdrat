using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Students;

namespace QdratNew.Services.Implementations
{
    public class StudentPracticeService : IStudentPracticeService
    {
        private readonly ApplicationDbContext _context;

        public StudentPracticeService(ApplicationDbContext context)
        {
            _context = context;
        }

        // 🟢 تدريب عام على المحاور الضعيفة
        public async Task<List<PracticeQuestionVm>> GetWeaknessPracticeAsync(int studentId, int count = 45)
        {
            // 1️⃣ محاولات الطالب من الاختبارات فقط
            var attempts = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                join ex in _context.Exams on a.ExamId equals ex.Id into exj
                from ex in exj.DefaultIfEmpty()
                where a.StudentId == studentId
                      && a.ExamAssignmentId != null
                      && a.HomeworkSetId == null
                      && a.PerformanceIndicatorExamId == null
                      && (ex == null ||
                          (ex.Type != ExamType.LevelAssessment &&
                           ex.Type != ExamType.PerformanceScale))
                select new
                {
                    s.Id,
                    s.Title,
                    a.IsCorrect
                }
            ).ToListAsync();

            if (!attempts.Any())
                return new List<PracticeQuestionVm>();

            // 2️⃣ حساب متوسط الأداء في كل محور
            var weakSections = attempts
                .GroupBy(x => new { x.Id, x.Title })
                .Select(g => new
                {
                    g.Key.Id,
                    g.Key.Title,
                    Accuracy = g.Count(x => x.IsCorrect) / (double)g.Count()
                })
                .Where(x => x.Accuracy < 0.5)
                .OrderBy(x => x.Accuracy)
                .Take(3)
                .Select(x => x.Id)
                .ToList();

            if (!weakSections.Any())
                return new List<PracticeQuestionVm>();

            // 3️⃣ الأسئلة الخاطئة في المحاور الضعيفة فقط
            var wrongQuestions = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions.Include(o => o.Options) on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join ex in _context.Exams on a.ExamId equals ex.Id into exj
                from ex in exj.DefaultIfEmpty()
                where a.StudentId == studentId
                      && !a.IsCorrect
                      && weakSections.Contains(l.SectionId)
                      && a.ExamAssignmentId != null
                      && a.HomeworkSetId == null
                      && a.PerformanceIndicatorExamId == null
                      && (ex == null ||
                          (ex.Type != ExamType.LevelAssessment &&
                           ex.Type != ExamType.PerformanceScale))
                select q
            ).Distinct().ToListAsync();

            int remaining = Math.Max(0, count - wrongQuestions.Count);

            // 4️⃣ أسئلة إضافية من نفس المحاور داخل الاختبارات العامة
            var extraQuestions = await (
                from eq in _context.ExamQuestions
                join ex in _context.Exams on eq.ExamId equals ex.Id
                join q in _context.Questions.Include(o => o.Options) on eq.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                where weakSections.Contains(l.SectionId)
                      && ex.Type != ExamType.LevelAssessment
                      && ex.Type != ExamType.PerformanceScale
                select q
            ).Distinct()
             .OrderBy(x => Guid.NewGuid())
             .Take(remaining)
             .ToListAsync();

            // 5️⃣ الدمج والترتيب النهائي
            var finalList = wrongQuestions
                .Select(q => new PracticeQuestionVm { Question = q, IsRepeatedMistake = true })
                .Concat(extraQuestions.Select(q => new PracticeQuestionVm { Question = q, IsRepeatedMistake = false }))
                .OrderBy(q => Guid.NewGuid())
                .Take(count)
                .ToList();

            return finalList;
        }

        // 🟢 تدريب على محور محدد فقط
        public async Task<List<PracticeQuestionVm>> GetWeaknessPracticeForSectionAsync(int studentId, int sectionId, int count = 45)
        {
            // 1️⃣ الأسئلة الخاطئة في هذا المحور من الاختبارات العامة فقط
            var wrongs = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions.Include(o => o.Options) on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join ex in _context.Exams on a.ExamId equals ex.Id into exj
                from ex in exj.DefaultIfEmpty()
                where a.StudentId == studentId
                      && !a.IsCorrect
                      && l.SectionId == sectionId
                      && a.ExamAssignmentId != null
                      && a.HomeworkSetId == null
                      && a.PerformanceIndicatorExamId == null
                      && (ex == null ||
                          (ex.Type != ExamType.LevelAssessment &&
                           ex.Type != ExamType.PerformanceScale))
                select q
            ).Distinct().ToListAsync();

            int remaining = Math.Max(0, count - wrongs.Count);

            // 2️⃣ أسئلة إضافية من نفس المحور داخل الاختبارات العامة
            var extras = await (
                from eq in _context.ExamQuestions
                join ex in _context.Exams on eq.ExamId equals ex.Id
                join q in _context.Questions.Include(o => o.Options) on eq.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                where l.SectionId == sectionId
                      && ex.Type != ExamType.LevelAssessment
                      && ex.Type != ExamType.PerformanceScale
                select q
            ).Distinct()
             .OrderBy(x => Guid.NewGuid())
             .Take(remaining)
             .ToListAsync();

            // 3️⃣ الدمج والترتيب
            var list = wrongs
                .Select(q => new PracticeQuestionVm { Question = q, IsRepeatedMistake = true })
                .Concat(extras.Select(q => new PracticeQuestionVm { Question = q, IsRepeatedMistake = false }))
                .OrderBy(x => Guid.NewGuid())
                .Take(count)
                .ToList();

            return list;
        }


        public async Task<List<PracticeQuestionVm>> GetMistakePracticeAsync(int studentId, int count = 20)
        {
            var ctx = _context;

            // ================================================
            // 🟢 1) تحديد كل الأسئلة التي ظهرت للطالب في أي اختبار
            // ================================================
            var shownQuestionIds = await (
                from eq in ctx.ExamQuestions

                    // 🔹 ربط اختبار الدفعة
                join ba in ctx.ExamAssignmentsToBatches
                    on eq.ExamAssignmentId equals ba.Id into baj
                from ba in baj.DefaultIfEmpty()

                    // 🔹 ربط اختبار الطالب الفردي
                join sa in ctx.ExamAssignmentsToStudents
                    on eq.ExamAssignmentToStudentId equals sa.Id into saj
                from sa in saj.DefaultIfEmpty()

                    // 🔹 ربط الطالب المسجَّل في الدفعة
                join enroll in ctx.StudentBatchEnrollments
                    on ba.BatchId equals enroll.BatchId into enr
                from enroll in enr.DefaultIfEmpty()

                where
                     // ⬅ اختبار الدفعة
                     (ba != null && enroll.StudentID == studentId)

                     // ⬅ اختبار الطالب الفردي
                     || (sa != null && sa.StudentId == studentId)

                select eq.QuestionId
            )
            .Distinct()
            .ToListAsync();

            // ================================================
            // 🟢 2) كل Attempts للطالب (إجابات + تخطيات)
            // ================================================
            var attempts = await ctx.QuestionAttemptNew
                .Where(a => a.StudentId == studentId)
                .ToListAsync();

            // ================================================
            // 🟢 3) تحديد الأسئلة المتخطّاة Skip
            // (لا يوجد لها Attempts إطلاقًا)
            // ================================================
            var skippedIds = shownQuestionIds
                .Where(qid => !attempts.Any(a => a.QuestionId == qid))
                .ToList();

            // ================================================
            // 🟢 4) تحديد الأسئلة الخاطئة
            // (الإجابة لا تطابق أي اختيار)
            // ================================================
            var wrongAttempts = attempts
                .Where(a =>
                    !ctx.QuestionOptions.Any(o =>
                        o.QuestionId == a.QuestionId &&
                        o.Text == a.SelectedAnswer)
                )
                .ToList();

            // ================================================
            // 🟢 5) تجميع كل الأسئلة (خطأ + تخطي)
            // بدون Contains — باستخدام Dictionary
            // ================================================
            var mistakeIds = skippedIds
                .Concat(wrongAttempts.Select(a => a.QuestionId))
                .Distinct()
                .ToList();

            var idLookup = mistakeIds.ToDictionary(id => id, id => true);

            // ================================================
            // 🟢 6) جلب الأسئلة نفسها من قاعدة البيانات
            // بدون Contains — بدون JOIN على قائمة
            // ================================================
            var allQuestions = await ctx.Questions
                .Include(q => q.Options)
                .ToListAsync();

            var mistakeQuestions = allQuestions
                .Where(q => idLookup.ContainsKey(q.Id))
                .ToList();

            // ================================================
            // 🟢 7) تجهيز ViewModel
            // ================================================
            var result = new List<PracticeQuestionVm>();

            foreach (var q in mistakeQuestions)
            {
                var attempt = wrongAttempts.FirstOrDefault(a => a.QuestionId == q.Id);

                bool isSkipped = attempt == null;

                result.Add(new PracticeQuestionVm
                {
                    Question = q,
                    StudentAnswer = attempt?.SelectedAnswer,
                    CorrectAnswer = q.CorrectAnswer,
                    IsSkipped = isSkipped,
                    IsRepeatedMistake = !isSkipped
                });
            }

            // ================================================
            // 🟢 8) إرجاع 20 سؤال عشوائي
            // ================================================
            return result
                .OrderBy(x => Guid.NewGuid())
                .Take(count)
                .ToList();
        }



    }
}
