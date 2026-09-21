using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Engines;
using System.Text.Json;
using Xunit;

namespace QdratNew.Tests
{
    // Sprint 4 (EE2) — Smoke test يُشغَّل يدويًا بعد كل نشر يمسّ ExamResultEngine أو أكشنز
    // Epic B: يشغّل GenerateSnapshotIfMissingAsync/ForIndividualAsync على عينة من الحالات
    // "المُصلَحة" (Note=NULL بعد ED2) ويتأكد أن كل Snapshot ناتج رياضيًا سليم.
    public class ExamReportSmokeTests
    {
        [Fact]
        public async Task GenerateSnapshotIfMissing_ForBatchAndIndividualSample_AllSnapshotsAreMathematicallyValid()
        {
            var dbName = Guid.NewGuid().ToString();
            var factory = new TestDbContextFactory(dbName);
            var engine = new ExamResultEngine(factory, NullLogger<ExamResultEngine>.Instance);

            (int examAssignmentId, int studentId) batchCase;
            (int examAssignmentToStudentId, int studentId) individualCase;

            using (var db = factory.CreateDbContext())
            {
                var exam = new Exam { Title = "اختبار دفعة", TotalQuestions = 4 };
                db.Exams.Add(exam);
                await db.SaveChangesAsync();

                var batchAssignment = new ExamAssignmentToBatch { ExamId = exam.Id, BatchId = 1, Title = "اختبار دفعة" };
                db.ExamAssignmentsToBatches.Add(batchAssignment);
                await db.SaveChangesAsync();

                var batchQuestions = SeedQuestions(db, 4);
                int order = 1;
                foreach (var q in batchQuestions)
                    db.ExamQuestions.Add(new ExamQuestion { QuestionId = q.Id, ExamId = exam.Id, ExamAssignmentId = batchAssignment.Id, Order = order++ });

                var batchStatus = new ExamStudentStatus
                {
                    StudentId = 501,
                    ExamId = exam.Id,
                    ExamAssignmentId = batchAssignment.Id,
                    IsSubmitted = true,
                    Status = ExamStatus.Completed,
                    Note = null // حالة "مُصلَحة" بعد ED2 — بانتظار إعادة الحساب
                };
                db.ExamStudentStatuses.Add(batchStatus);

                foreach (var q in batchQuestions.Take(3))
                {
                    db.QuestionAttemptNew.Add(new QuestionAttemptNew
                    {
                        StudentId = 501,
                        QuestionId = q.Id,
                        ExamAssignmentId = batchAssignment.Id,
                        IsCorrect = true,
                        SelectedAnswer = "A",
                        AttemptedAt = DateTime.UtcNow
                    });
                }

                var indivExam = new Exam { Title = "اختبار فردي", TotalQuestions = 3 };
                db.Exams.Add(indivExam);
                await db.SaveChangesAsync();

                var indivAssignment = new ExamAssignmentToStudent
                {
                    ExamId = indivExam.Id,
                    StudentId = 502,
                    ScheduledDate = DateTime.UtcNow.AddHours(-2),
                    EndAt = DateTime.UtcNow.AddHours(-1),
                    DurationMinutes = 20
                };
                db.ExamAssignmentsToStudents.Add(indivAssignment);
                await db.SaveChangesAsync();

                var indivQuestions = SeedQuestions(db, 3);
                order = 1;
                foreach (var q in indivQuestions)
                    db.ExamQuestions.Add(new ExamQuestion { QuestionId = q.Id, ExamId = indivExam.Id, ExamAssignmentToStudentId = indivAssignment.Id, Order = order++ });

                var indivStatus = new ExamStudentStatus
                {
                    StudentId = 502,
                    ExamId = indivExam.Id,
                    ExamAssignmentToStudentId = indivAssignment.Id,
                    IsSubmitted = true,
                    Status = ExamStatus.Completed,
                    Note = null
                };
                db.ExamStudentStatuses.Add(indivStatus);

                // محاولات يتيمة زيادة عن عدد الأسئلة — نفس فئة حادثة 2116، للتأكد أن الـ Smoke
                // Test يمسك أي تراجع مستقبلي يعيد نفس الخطأ.
                foreach (var q in indivQuestions)
                {
                    db.QuestionAttemptNew.Add(new QuestionAttemptNew
                    {
                        StudentId = 502,
                        QuestionId = q.Id,
                        ExamAssignmentToStudentId = indivAssignment.Id,
                        IsCorrect = true,
                        SelectedAnswer = "A",
                        AttemptedAt = DateTime.UtcNow
                    });
                }
                for (int i = 0; i < 10; i++)
                {
                    db.QuestionAttemptNew.Add(new QuestionAttemptNew
                    {
                        StudentId = 502,
                        QuestionId = Guid.NewGuid(),
                        ExamAssignmentToStudentId = indivAssignment.Id,
                        IsCorrect = true,
                        SelectedAnswer = "A",
                        AttemptedAt = DateTime.UtcNow
                    });
                }

                await db.SaveChangesAsync();

                batchCase = (batchAssignment.Id, 501);
                individualCase = (indivAssignment.Id, 502);
            }

            await engine.GenerateSnapshotIfMissingAsync(batchCase.examAssignmentId, batchCase.studentId);
            await engine.GenerateSnapshotIfMissingForIndividualAsync(individualCase.examAssignmentToStudentId, individualCase.studentId);

            using (var verifyDb = factory.CreateDbContext())
            {
                var statuses = await verifyDb.ExamStudentStatuses
                    .Where(s => s.StudentId == 501 || s.StudentId == 502)
                    .ToListAsync();

                Assert.Equal(2, statuses.Count);

                foreach (var status in statuses)
                {
                    Assert.False(string.IsNullOrEmpty(status.Note));
                    var snapshot = JsonSerializer.Deserialize<QdratNew.DTOs.Exams.ExamFinalResultDto>(status.Note!)!;

                    Assert.True(snapshot.ScorePercent <= 100, $"ScorePercent تجاوز 100 لطالب {status.StudentId}");
                    Assert.True(snapshot.ScorePercent >= 0);
                    Assert.True(snapshot.Skipped >= 0, $"Skipped سالب لطالب {status.StudentId}");
                    Assert.True(snapshot.Wrong >= 0, $"Wrong سالب لطالب {status.StudentId}");
                    Assert.True(snapshot.Correct <= snapshot.TotalQuestions, $"Correct أكبر من TotalQuestions لطالب {status.StudentId}");
                }
            }
        }

        private static List<Question> SeedQuestions(ApplicationDbContext db, int count)
        {
            var list = new List<Question>();
            for (int i = 0; i < count; i++)
            {
                var q = new Question
                {
                    Id = Guid.NewGuid(),
                    Title = $"سؤال {i}",
                    ReferenceNumber = $"SM-{Guid.NewGuid():N}",
                    CurriculumId = 1,
                    LessonId = 1,
                    SectionId = 1
                };
                db.Questions.Add(q);
                list.Add(q);
            }
            return list;
        }
    }
}
