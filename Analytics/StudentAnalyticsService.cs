using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.Students;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Analytics
{
    public class StudentAnalyticsService : IStudentAnalyticsService
    {
        private readonly ApplicationDbContext _context;

        public StudentAnalyticsService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ 1. تسجيل نقاط الضعف بناءً على إجابات الطالب
        public async Task RecordWeaknessAsync(int studentId, Guid questionId, bool isCorrect)
        {
            try
            {
                // ⚙️ جلب السؤال والدرس المرتبط به
                var question = await _context.Questions
                    .Include(q => q.Lesson)
                    .FirstOrDefaultAsync(q => q.Id == questionId);

                if (question == null || question.LessonId == 0)
                    return;

                var lessonId = question.LessonId;

                // لو الطالب جاوب صح → إلغاء أي ضعف سابق
                if (isCorrect)
                {
                    var existing = await _context.StudentWeaknesses
                        .FirstOrDefaultAsync(w => w.StudentId == studentId && w.LessonId == lessonId && !w.IsResolved);

                    if (existing != null)
                    {
                        existing.IsResolved = true;
                        existing.LastUpdated = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                    return;
                }

                // لو الطالب أخطأ → سجّل أو حدّث الضعف
                var weakness = await _context.StudentWeaknesses
                    .FirstOrDefaultAsync(w => w.StudentId == studentId && w.LessonId == lessonId);

                if (weakness == null)
                {
                    weakness = new StudentWeakness
                    {
                        StudentId = studentId,
                        LessonId = lessonId,
                        MistakeCount = 1,
                        OccurrenceCount = 1,
                        IsResolved = false,
                        WeaknessSource = "Exam/Homework",
                        FirstDetectedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };
                    await _context.StudentWeaknesses.AddAsync(weakness);
                }
                else
                {
                    weakness.MistakeCount += 1;
                    weakness.OccurrenceCount += 1;
                    weakness.LastUpdated = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ RecordWeaknessAsync Error: {ex.Message}");
            }
        }
        public async Task RecordAttendanceAsync(int studentId, int lectureId, bool isPresent)
        {
            // 🧠 تسجيل النشاط في سجل النشاطات
            _context.StudentActivityLogs.Add(new StudentActivityLog
            {
                StudentId = studentId,
                ActivityType = "Attendance",
                ActivityTitle = isPresent ? "حضور محاضرة" : "غياب عن محاضرة",
                Timestamp = DateTime.UtcNow,
                Source = "Lecture",
                LectureId = lectureId
            });

            // 🔹 تحديث معدل المشاركة في StudentPerformance
            var perf = await _context.StudentPerformances
                .FirstOrDefaultAsync(p => p.StudentID == studentId);

            if (perf == null)
            {
                perf = new StudentPerformance
                {
                    StudentID = studentId,
                    EngagementScore = isPresent ? 1 : 0,
                    ExamDate = DateTime.UtcNow
                };
                _context.StudentPerformances.Add(perf);
            }
            else
            {
                double change = isPresent ? 1 : -1;
                perf.EngagementScore = Math.Clamp(perf.EngagementScore + change, 0, 100);
            }

            await _context.SaveChangesAsync();
        }

        // ✅ 2. تسجيل نتائج التدريب العلاجي
        public async Task RecordRemedialTrainingAsync(int studentId, int sectionId, int totalQuestions, int correctAnswers)
        {
            try
            {
                if (totalQuestions <= 0) return;

                double scorePercentage = Math.Round(correctAnswers * 100.0 / totalQuestions, 1);

                var record = new StudentWeaknessTraining
                {
                    StudentId = studentId,
                    SectionId = sectionId,
                    TotalQuestions = totalQuestions,
                    CorrectAnswers = correctAnswers,
                    ScorePercentage = scorePercentage,
                    TrainedAt = DateTime.Now
                };

                await _context.StudentWeaknessTrainings.AddAsync(record);
                await _context.SaveChangesAsync();

                if (scorePercentage >= 70)
                {
                    var related = await _context.StudentWeaknesses
                        .Include(w => w.Lesson)
                        .Where(w => w.StudentId == studentId && w.Lesson.SectionId == sectionId && !w.IsResolved)
                        .ToListAsync();

                    foreach (var w in related)
                    {
                        w.IsResolved = true;
                        w.LastUpdated = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ RecordRemedialTrainingAsync Error: {ex.Message}");
            }
        }


        // ✅ نسخة ذكية تحسب التقدم تلقائيًا بناءً على إجابات الطالب
        public async Task UpdateProgressAsync(int studentId, int curriculumId)
        {
            try
            {
                // 🟢 احسب نسبة التقدم من عدد الأسئلة المجابة الصحيحة في المنهج
                var totalQuestions = await (
                    from q in _context.Questions
                    join l in _context.Lessons on q.LessonId equals l.Id
                    join s in _context.Sections on l.SectionId equals s.Id
                    where s.CurriculumId == curriculumId
                    select q.Id
                ).CountAsync();

                var correctAnswers = await (
                    from a in _context.QuestionAttemptNew
                    join q in _context.Questions on a.QuestionId equals q.Id
                    join l in _context.Lessons on q.LessonId equals l.Id
                    join s in _context.Sections on l.SectionId equals s.Id
                    where a.StudentId == studentId &&
                          a.IsCorrect &&
                          s.CurriculumId == curriculumId
                    select a.Id
                ).CountAsync();

                double progress = totalQuestions > 0
                    ? Math.Round(correctAnswers * 100.0 / totalQuestions, 2)
                    : 0.0;

                // 🟢 استدعاء الدالة الأصلية
                await UpdateProgressAsync(studentId, curriculumId, progress);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ UpdateProgressAsync (auto) Error: {ex.Message}");
            }
        }


        // ✅ تحديث ترتيب الطالب داخل الدفعة بعد كل اختبار أو واجب
        public async Task UpdateRankHistoryAsync(int studentId, int batchId)
        {
            try
            {
                // 🟢 جلب جميع الطلاب في نفس الدفعة
                var batchStudents = await _context.StudentBatchEnrollments
                    .Where(s => s.BatchId == batchId)
                    .Select(s => s.StudentID)
                    .ToListAsync();

                if (!batchStudents.Any())
                    return;

                // 🟢 حساب متوسط الدرجات لكل طالب في الدفعة
                var averages = await _context.QuestionAttemptNew
                    .Where(a => batchStudents.Contains(a.StudentId))
                    .GroupBy(a => a.StudentId)
                    .Select(g => new
                    {
                        StudentId = g.Key,
                        AverageScore = g.Any()
                            ? g.Average(x => x.IsCorrect ? 100.0 : 0.0)
                            : 0.0
                    })
                    .OrderByDescending(x => x.AverageScore)
                    .ToListAsync();

                int total = averages.Count;

                // 🟢 إزالة السجلات القديمة الاختيارية (حسب احتياجك)
                var oldRecords = _context.StudentRankHistories.Where(r => r.BatchId == batchId);
                _context.StudentRankHistories.RemoveRange(oldRecords);

                // 🟢 تسجيل الترتيب الجديد
                for (int i = 0; i < averages.Count; i++)
                {
                    var record = new StudentRankHistory
                    {
                        StudentId = averages[i].StudentId,
                        BatchId = batchId,
                        Rank = i + 1,
                        TotalStudents = total,
                        RecordedAt = DateTime.Now
                    };
                    await _context.StudentRankHistories.AddAsync(record);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ UpdateRankHistoryAsync Error: {ex.Message}");
            }
        }



        // ✅ 3. تحديث نسبة التقدم في المنهج
        public async Task UpdateProgressAsync(int studentId, int curriculumId, double progress)
        {
            try
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentID == studentId);

                var courseId = await _context.Curriculums
                    .Where(c => c.Id == curriculumId)
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync();

                var course = await _context.Courses.FindAsync(courseId);

                var existing = await _context.StudentProgress
                    .FirstOrDefaultAsync(p => p.StudentID == studentId && p.CourseID == courseId);

                if (existing == null)
                {
                    var newProgress = new StudentProgress
                    {
                        StudentID = studentId,
                        Student = student!,
                        CourseID = courseId,
                        Course = course!,
                        Date = DateTime.UtcNow,
                        CompletedLessons = 0,
                        CompletedExercises = 0,
                        ProgressPercentage = progress,
                        Topic = "منهج " + curriculumId,
                        Score = progress,
                        DifficultyLevel = "متوسط"
                    };
                    await _context.StudentProgress.AddAsync(newProgress);
                }
                else
                {
                    existing.ProgressPercentage = progress;
                    existing.LastUpdated = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ UpdateProgressAsync Error: {ex.Message}");
            }
        }

        // ✅ 4. تسجيل ترتيب الطالب داخل الدفعة
        public async Task RecordRankAsync(int studentId, int batchId)
        {
            try
            {
                var batchStudents = await _context.StudentBatchEnrollments
                    .Where(s => s.BatchId == batchId)
                    .Select(s => s.StudentID)
                    .ToListAsync();

                if (!batchStudents.Any()) return;

                var scores = await _context.StudentPerformances
                    .Where(sp => batchStudents.Contains(sp.StudentID))
                    .GroupBy(sp => sp.StudentID)
                    .Select(g => new { StudentId = g.Key, AverageScore = g.Average(x => x.Score) })
                    .OrderByDescending(x => x.AverageScore)
                    .ToListAsync();

                int total = scores.Count;

                for (int i = 0; i < scores.Count; i++)
                {
                    var s = scores[i];
                    await _context.StudentRankHistories.AddAsync(new StudentRankHistory
                    {
                        StudentId = s.StudentId,
                        BatchId = batchId,
                        Rank = i + 1,
                        TotalStudents = total,
                        RecordedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ RecordRankAsync Error: {ex.Message}");
            }
        }

        // ✅ 5. تحليل تعلم الطالب في منهج محدد
        public async Task<StudentLearningAnalyticsDto> AnalyzeStudentAsync(int studentId, int curriculumId)
        {
            var attempts = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                where a.StudentId == studentId && s.CurriculumId == curriculumId
                select new { a.IsCorrect, q.IsQuantitative }
            ).ToListAsync();

            return new StudentLearningAnalyticsDto
            {
                StudentId = studentId,
                CurriculumId = curriculumId,
                TotalAttempts = attempts.Count,
                CorrectAttempts = attempts.Count(x => x.IsCorrect),
                QuantitativeAccuracy = attempts.Any(x => x.IsQuantitative)
                    ? Math.Round(attempts.Where(x => x.IsQuantitative).Count(y => y.IsCorrect) * 100.0 / attempts.Count(z => z.IsQuantitative), 2)
                    : 0,
                VerbalAccuracy = attempts.Any(x => !x.IsQuantitative)
                    ? Math.Round(attempts.Where(x => !x.IsQuantitative).Count(y => y.IsCorrect) * 100.0 / attempts.Count(z => !z.IsQuantitative), 2)
                    : 0
            };
        }

        // ✅ 6. تحليل الأداء العام للطالب
        public async Task<PerformanceAnalysisResult> AnalyzePerformanceAsync(int studentId)
        {
            var performance = await (
                from sp in _context.StudentPerformances
                join sec in _context.Sections on sp.SectionId equals sec.Id
                where sp.StudentID == studentId
                group sp by sec.Title into g
                select new
                {
                    Section = g.Key,
                    Accuracy = g.Average(x => x.Score)
                }
            ).ToListAsync();

            var strengths = performance.OrderByDescending(x => x.Accuracy).Take(2).Select(x => x.Section).ToList();
            var weaknesses = performance.OrderBy(x => x.Accuracy).Take(3).Select(x => x.Section).ToList();

            return new PerformanceAnalysisResult
            {
                StudentId = studentId,
                OverallScore = performance.Any() ? performance.Average(x => x.Accuracy) : 0,
                Strengths = strengths,
                Weaknesses = weaknesses
            };
        }



        public async Task<int> GetCurriculumIdByHomeworkAsync(int homeworkSetId)
        {
            try
            {
                var curriculumId = await (
                    from hs in _context.HomeworkSets
                    where hs.Id == homeworkSetId
                    select hs.CurriculumId
                ).FirstOrDefaultAsync();

                return curriculumId ?? 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ GetCurriculumIdByHomeworkAsync Error: {ex.Message}");
                return 0;
            }
        }




    }
}
