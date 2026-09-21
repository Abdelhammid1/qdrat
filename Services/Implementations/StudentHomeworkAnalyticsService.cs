using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Homework;

namespace QdratNew.Services.Implementations
{
    public class StudentHomeworkAnalyticsService : IStudentHomeworkAnalyticsService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public StudentHomeworkAnalyticsService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ✅ تحليل واجب معين للطالب
        public async Task<HomeworkAnalyticsVm> AnalyzeHomeworkAsync(int studentId, int homeworkSetId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var vm = new HomeworkAnalyticsVm
            {
                StudentId = studentId,
                HomeworkSetId = homeworkSetId
            };

            // 🟩 1️⃣ جلب الأسئلة + المنهج (مهم جدًا للـ RTL)
            var homeworks = await (
                from h in _context.Homeworks.AsNoTracking()
                join q in _context.Questions
                    .Include(q => q.Curriculum) // ✅ أهم إضافة
                    on h.QuestionId equals q.Id
                where h.StudentId == studentId && h.HomeworkSetId == homeworkSetId
                group new { h, q } by new
                {
                    h.QuestionId,
                    q.Title,
                    q.CorrectAnswer,
                    IsRTL = q.Curriculum != null ? q.Curriculum.IsRTL : true
                } into g
                select new
                {
                    g.Key.QuestionId,
                    QuestionTitle = g.Key.Title,
                    CorrectAnswer = g.Key.CorrectAnswer,
                    IsRTL = g.Key.IsRTL
                }
            ).ToListAsync();

            vm.TotalQuestions = homeworks.Count;

            // 🟩 2️⃣ جلب المحاولات
            var attempts = await _context.QuestionAttemptNew.AsNoTracking()
                .Where(a => a.StudentId == studentId && a.HomeworkSetId == homeworkSetId)
                .ToListAsync();

            // 🟩 3️⃣ إصلاح HomeworkSetId القديم
            if (attempts.Any(a => a.HomeworkSetId == null))
            {
                var missing = await (
                    from a in _context.QuestionAttemptNew.AsNoTracking()
                    join h in _context.Homeworks
                        on new { a.StudentId, a.QuestionId } equals new { h.StudentId, h.QuestionId }
                    where a.HomeworkSetId == null && h.StudentId == studentId
                    select new { a, h.HomeworkSetId }
                ).ToListAsync();

                foreach (var m in missing)
                    m.a.HomeworkSetId = m.HomeworkSetId;

                await _context.SaveChangesAsync();
            }

            // 🟩 4️⃣ الحسابات
            int correct = 0, wrong = 0, skipped = 0;
            double totalSeconds = 0;

            foreach (var hw in homeworks)
            {
                var attempt = attempts.FirstOrDefault(a => a.QuestionId == hw.QuestionId);

                if (attempt == null)
                {
                    skipped++;
                    continue;
                }

                totalSeconds += attempt.TimeTakenSeconds;

                if (attempt.IsCorrect) correct++;
                else wrong++;

                vm.Questions.Add(new HomeworkQuestionResultItem
                {
                    QuestionId = hw.QuestionId,
                    QuestionTitle = hw.QuestionTitle,
                    StudentAnswer = attempt.SelectedAnswer,
                    CorrectAnswer = hw.CorrectAnswer,
                    IsCorrect = attempt.IsCorrect,
                    TimeTakenSeconds = attempt.TimeTakenSeconds,

                    // ✅ أهم إضافة
                    IsRTL = hw.IsRTL
                });
            }

            // 🟩 5️⃣ الحساب العام
            vm.CorrectCount = correct;
            vm.WrongCount = wrong;
            vm.SkippedCount = skipped;

            vm.ScorePercentage = vm.TotalQuestions > 0
                ? Math.Round(correct * 100.0 / vm.TotalQuestions, 2)
                : 0;

            // 🟩 6️⃣ حساب الوقت
            var attemptTimes = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.HomeworkSetId == homeworkSetId)
                .Select(a => a.AttemptedAt)
                .ToListAsync();

            if (attemptTimes.Any())
            {
                var start = attemptTimes.Min();
                var end = attemptTimes.Max();

                var actualMinutes = (int)Math.Round((end - start).TotalMinutes);
                if (actualMinutes <= 0)
                    actualMinutes = 1;

                vm.TimeSpentMinutes = actualMinutes;
                vm.TimeExplanation = $"⏱ الوقت من {start:HH:mm} إلى {end:HH:mm}";
            }
            else
            {
                vm.TimeSpentMinutes = Math.Round(totalSeconds / 60.0, 1);
                vm.TimeExplanation = "⏱ الوقت تقديري";
            }

            // 🟩 7️⃣ Sections
            vm.SectionsPerformance = await (
                from a in _context.QuestionAttemptNew.AsNoTracking()
                join q in _context.Questions
                    .Include(q => q.Curriculum) // ✅
                    on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                where a.StudentId == studentId && a.HomeworkSetId == homeworkSetId
                group new { a, q } by new { s.Id, s.Title, q.Curriculum.IsRTL } into g
                select new SectionPerformanceVm
                {
                    SectionId = g.Key.Id,
                    SectionTitle = g.Key.Title,
                    Accuracy = g.Count() > 0
                        ? Math.Round(g.Count(x => x.a.IsCorrect) * 100.0 / g.Count(), 2)
                        : 0
                }
            ).ToListAsync();

            // 🟩 8️⃣ Lessons
            vm.LessonsPerformance = await (
                from a in _context.QuestionAttemptNew.AsNoTracking()
                join q in _context.Questions
                    .Include(q => q.Curriculum) // ✅
                    on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                where a.StudentId == studentId && a.HomeworkSetId == homeworkSetId
                group new { a, q } by new { l.Id, l.Title, q.Curriculum.IsRTL } into g
                select new LessonPerformanceVm
                {
                    LessonId = g.Key.Id,
                    LessonTitle = g.Key.Title,
                    TotalQuestions = g.Count(),
                    SuccessRate = g.Count() > 0
                        ? Math.Round(g.Count(x => x.a.IsCorrect) * 100.0 / g.Count(), 1)
                        : 0
                }
            ).ToListAsync();

            return vm;
        }
        // ✅ تحديث حالة الطالب داخل HomeworkSetStudent (آمن ضد المفاتيح الأجنبية)
        public async Task UpdateHomeworkSubmissionAsync(int studentId, int homeworkSetId, double score, bool isSubmitted)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🔒 تحقق أولاً أن الواجب موجود فعليًا
            var exists = await _context.HomeworkSets.AsNoTracking().AnyAsync(x => x.Id == homeworkSetId);
            if (!exists)
            {
                Console.WriteLine($"⚠️ محاولة تحديث واجب غير موجود (HomeworkSetId = {homeworkSetId}) — تم تجاهل العملية.");
                return;
            }

            var link = await _context.HomeworkSetStudents
                .FirstOrDefaultAsync(x => x.StudentId == studentId && x.HomeworkSetId == homeworkSetId);

            if (link == null)
            {
                link = new HomeworkSetStudent
                {
                    StudentId = studentId,
                    HomeworkSetId = homeworkSetId,
                    IsSubmitted = isSubmitted,
                    Score = score,
                    SubmittedAt = isSubmitted ? DateTime.Now : null,
                    LastUpdated = DateTime.Now
                };
                _context.HomeworkSetStudents.Add(link);
            }
            else
            {
                link.IsSubmitted = isSubmitted;
                link.Score = score;
                link.SubmittedAt = isSubmitted ? DateTime.Now : link.SubmittedAt;
                link.LastUpdated = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        // ✅ عند إرسال واجب جديد: تسجيل كل طلاب الدفعة في HomeworkSetStudent (بشكل آمن)
        public async Task EnsureStudentsLinkedToHomeworkSetAsync(int homeworkSetId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var set = await _context.HomeworkSets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == homeworkSetId);
            if (set == null)
            {
                Console.WriteLine($"⚠️ لا يوجد واجب رئيسي بالرقم {homeworkSetId} في جدول HomeworkSets — تم تجاهل العملية.");
                return;
            }

            var studentIds = await _context.StudentBatchEnrollments
                .Where(e => e.BatchId == set.BatchId)
                .Select(e => e.StudentID)
                .ToListAsync();

            if (!studentIds.Any()) return;

            var existingLinks = await _context.HomeworkSetStudents
                .Where(x => x.HomeworkSetId == homeworkSetId)
                .Select(x => x.StudentId)
                .ToListAsync();

            var newLinks = studentIds.Except(existingLinks).ToList();
            if (!newLinks.Any()) return;

            foreach (var sid in newLinks)
            {
                _context.HomeworkSetStudents.Add(new HomeworkSetStudent
                {
                    StudentId = sid,
                    HomeworkSetId = homeworkSetId,
                    AssignedAt = DateTime.Now,
                    IsSubmitted = false,
                    Score = null
                });
            }

            await _context.SaveChangesAsync();
        }

        // ✅ دالة إضافية: ربط طالب محدد بجميع الواجبات في دفعته (آمنة ضد المفاتيح الأجنبية)
        // ✅ ربط طالب محدد بالواجبات المناسبة فقط (بعد تاريخ التحاقه بالدفعة)
        public async Task EnsureStudentsLinkedToHomeworkSetAsyncForStudent(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🟢 جلب الدفعات التي ينتمي إليها الطالب مع تاريخ التحاقه
            var enrollments = await _context.StudentBatchEnrollments
                .Where(e => e.StudentID == studentId)
                .Select(e => new { e.BatchId, e.EnrolledAt })
                .ToListAsync();

            if (!enrollments.Any())
                return;

            foreach (var enroll in enrollments)
            {
                var batchId = enroll.BatchId;
                var joinDate = enroll.EnrolledAt;

                // 🟢 جلب الواجبات النشطة فقط للدفعة (بعد التحاق الطالب)
                var homeworkSets = await _context.HomeworkSets
         .Where(hs =>
             hs.BatchId == batchId &&
             (
                 // الواجب بدأ بعد التحاق الطالب
                 hs.StartAt >= joinDate ||

                 // أو الواجب بدأ قبل التحاقه لكنه ما زال مفتوحًا عند التحاقه
                 (hs.EndAt > joinDate && hs.EndAt >= DateTime.Now)
             )
         )
         .Select(hs => hs.Id)
         .ToListAsync();


                if (!homeworkSets.Any())
                    continue;

                // 🟢 استبعاد الواجبات المرتبطة مسبقًا
                var existingLinks = await _context.HomeworkSetStudents
                    .Where(x => x.StudentId == studentId)
                    .Select(x => x.HomeworkSetId)
                    .ToListAsync();

                var newSets = homeworkSets.Except(existingLinks).ToList();
                if (!newSets.Any())
                    continue;

                foreach (var setId in newSets)
                {
                    _context.HomeworkSetStudents.Add(new HomeworkSetStudent
                    {
                        StudentId = studentId,
                        HomeworkSetId = setId,
                        AssignedAt = DateTime.Now,
                        IsSubmitted = false,
                        Score = null
                    });
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
