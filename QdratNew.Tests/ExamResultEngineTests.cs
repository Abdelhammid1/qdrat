using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Enums.Abstractions;
using QdratNew.Services.Exams.Abstractions;
using QdratNew.Services.Exams.Engines;
using System.Text.Json;
using Xunit;

namespace QdratNew.Tests
{
    // Sprint 4 (EA3) — اختبارات وحدة لـ ExamResultEngine.FinalizeAsync، الغرض منع رجوع نفس
    // فئة حادثة examAssignmentId=2116 (نسبة > 100%، Skipped سالب، محور "غير معروف").
    public class ExamResultEngineTests
    {
        private static ExamResultEngine CreateEngine(string dbName)
            => new ExamResultEngine(new TestDbContextFactory(dbName), NullLogger<ExamResultEngine>.Instance);

        private static async Task<(ExamAssignmentToStudent assignment, List<Question> questions)> SeedIndividualExamAsync(
            ApplicationDbContext db, int studentId, int questionCount)
        {
            var exam = new Exam { Title = "اختبار تجريبي", TotalQuestions = questionCount };
            db.Exams.Add(exam);
            await db.SaveChangesAsync();

            var assignment = new ExamAssignmentToStudent
            {
                ExamId = exam.Id,
                StudentId = studentId,
                ScheduledDate = DateTime.UtcNow.AddHours(-1),
                EndAt = DateTime.UtcNow.AddHours(1),
                DurationMinutes = 30
            };
            db.ExamAssignmentsToStudents.Add(assignment);

            var status = new ExamStudentStatus
            {
                StudentId = studentId,
                ExamId = exam.Id,
                ExamAssignmentToStudentId = null // يُربط لاحقًا بعد الحفظ للحصول على Id
            };

            await db.SaveChangesAsync();

            status.ExamAssignmentToStudentId = assignment.Id;
            db.ExamStudentStatuses.Add(status);
            await db.SaveChangesAsync();

            var questions = new List<Question>();
            for (int i = 0; i < questionCount; i++)
            {
                var q = new Question
                {
                    Id = Guid.NewGuid(),
                    Title = $"سؤال {i}",
                    ReferenceNumber = $"Q-{i}",
                    CurriculumId = 1,
                    LessonId = 1,
                    SectionId = 1
                };
                db.Questions.Add(q);
                questions.Add(q);
            }
            await db.SaveChangesAsync();

            int order = 1;
            foreach (var q in questions)
            {
                db.ExamQuestions.Add(new ExamQuestion
                {
                    QuestionId = q.Id,
                    ExamId = exam.Id,
                    ExamAssignmentToStudentId = assignment.Id,
                    Order = order++
                });
            }
            await db.SaveChangesAsync();

            return (assignment, questions);
        }

        [Fact]
        public async Task FinalizeAsync_MatchedAttempts_ProducesCorrectScoreAndNoSkippedBelowZero()
        {
            // سيناريو 1: تطابق طبيعي — عدد الأسئلة = عدد المحاولات.
            var dbName = Guid.NewGuid().ToString();
            var engine = CreateEngine(dbName);

            using (var db = new TestDbContextFactory(dbName).CreateDbContext())
            {
                var (assignment, questions) = await SeedIndividualExamAsync(db, studentId: 1, questionCount: 10);

                for (int i = 0; i < questions.Count; i++)
                {
                    db.QuestionAttemptNew.Add(new QuestionAttemptNew
                    {
                        StudentId = 1,
                        QuestionId = questions[i].Id,
                        ExamAssignmentToStudentId = assignment.Id,
                        IsCorrect = i < 7, // 7 صحيحة من 10
                        SelectedAnswer = "A",
                        AttemptedAt = DateTime.UtcNow
                    });
                }
                await db.SaveChangesAsync();

                var context = new ExamResultContext
                {
                    StudentId = 1,
                    ExamAssignmentToStudentId = assignment.Id,
                    Kind = ExamKind.General
                };

                await engine.FinalizeAsync(context, reviewSeconds: 60);
            }

            using (var verifyDb = new TestDbContextFactory(dbName).CreateDbContext())
            {
                var status = await verifyDb.ExamStudentStatuses.FirstAsync(s => s.StudentId == 1);
                var snapshot = JsonSerializer.Deserialize<QdratNew.DTOs.Exams.ExamFinalResultDto>(status.Note!)!;

                Assert.Equal(10, snapshot.TotalQuestions);
                Assert.Equal(7, snapshot.Correct);
                Assert.Equal(3, snapshot.Wrong);
                Assert.Equal(0, snapshot.Skipped);
                Assert.Equal(70, snapshot.ScorePercent);
                Assert.True(status.IsSubmitted);
            }
        }

        [Fact]
        public async Task FinalizeAsync_OrphanedAttemptsExceedQuestionCount_NeverProducesImpossibleNumbers()
        {
            // سيناريو 2: محاكاة حادثة الإنتاج 2116 — 15 سؤال حالي، 74 محاولة (بعضها لأسئلة لم
            // تعد جزءًا من الاختبار)، 17 صحيحة. يجب ألا تظهر نسبة > 100% ولا Skipped سالب ولا
            // قسم بمعرّف 0 خارج الأسئلة الفعلية بلا قسم.
            var dbName = Guid.NewGuid().ToString();
            var engine = CreateEngine(dbName);

            using (var db = new TestDbContextFactory(dbName).CreateDbContext())
            {
                var (assignment, questions) = await SeedIndividualExamAsync(db, studentId: 2, questionCount: 15);

                // 15 محاولة على الأسئلة الفعلية، 17 منها صحيحة يُستحيل تحقيقها فعليًا بمحاولة واحدة
                // لكل سؤال — لذا نجعل أول 15 محاولة على الأسئلة الحقيقية (10 صحيحة)، والباقي (59
                // محاولة، منها 7 "صحيحة" يتيمة) على أسئلة يتيمة غير موجودة في ExamQuestions.
                for (int i = 0; i < questions.Count; i++)
                {
                    db.QuestionAttemptNew.Add(new QuestionAttemptNew
                    {
                        StudentId = 2,
                        QuestionId = questions[i].Id,
                        ExamAssignmentToStudentId = assignment.Id,
                        IsCorrect = i < 10,
                        SelectedAnswer = "A",
                        AttemptedAt = DateTime.UtcNow
                    });
                }

                for (int i = 0; i < 59; i++)
                {
                    db.QuestionAttemptNew.Add(new QuestionAttemptNew
                    {
                        StudentId = 2,
                        QuestionId = Guid.NewGuid(), // سؤال يتيم — غير موجود في ExamQuestions للتكليف
                        ExamAssignmentToStudentId = assignment.Id,
                        IsCorrect = i < 7,
                        SelectedAnswer = "A",
                        AttemptedAt = DateTime.UtcNow
                    });
                }

                await db.SaveChangesAsync();

                var context = new ExamResultContext
                {
                    StudentId = 2,
                    ExamAssignmentToStudentId = assignment.Id,
                    Kind = ExamKind.General
                };

                await engine.FinalizeAsync(context, reviewSeconds: 60);
            }

            using (var verifyDb = new TestDbContextFactory(dbName).CreateDbContext())
            {
                var status = await verifyDb.ExamStudentStatuses.FirstAsync(s => s.StudentId == 2);
                var snapshot = JsonSerializer.Deserialize<QdratNew.DTOs.Exams.ExamFinalResultDto>(status.Note!)!;

                Assert.Equal(15, snapshot.TotalQuestions);
                Assert.True(snapshot.Correct <= snapshot.TotalQuestions);
                Assert.Equal(10, snapshot.Correct);
                Assert.True(snapshot.Skipped >= 0);
                Assert.Equal(0, snapshot.Skipped);
                Assert.True(snapshot.ScorePercent <= 100);

                // القسم 1 هو القسم الحقيقي الوحيد للأسئلة الفعلية — المحاولات اليتيمة لا يجب أن
                // تُنشئ أي قسم "غير معروف" (sectionId = 0) لأنها استُبعدت قبل التجميع بالأقسام.
                Assert.DoesNotContain(0, snapshot.Sections.Keys);
            }
        }

        [Fact]
        public async Task FinalizeAsync_NoAttempts_AllSkippedNoException()
        {
            // سيناريو 3: اختبار بلا أي محاولات إطلاقًا.
            var dbName = Guid.NewGuid().ToString();
            var engine = CreateEngine(dbName);

            using (var db = new TestDbContextFactory(dbName).CreateDbContext())
            {
                var (assignment, _) = await SeedIndividualExamAsync(db, studentId: 3, questionCount: 5);

                var context = new ExamResultContext
                {
                    StudentId = 3,
                    ExamAssignmentToStudentId = assignment.Id,
                    Kind = ExamKind.General
                };

                var exception = await Record.ExceptionAsync(() => engine.FinalizeAsync(context, reviewSeconds: 0));
                Assert.Null(exception);
            }

            using (var verifyDb = new TestDbContextFactory(dbName).CreateDbContext())
            {
                var status = await verifyDb.ExamStudentStatuses.FirstAsync(s => s.StudentId == 3);
                var snapshot = JsonSerializer.Deserialize<QdratNew.DTOs.Exams.ExamFinalResultDto>(status.Note!)!;

                Assert.Equal(5, snapshot.TotalQuestions);
                Assert.Equal(0, snapshot.Correct);
                Assert.Equal(5, snapshot.Skipped);
                Assert.Equal(0, snapshot.ScorePercent);
            }
        }
    }
}
