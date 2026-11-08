using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using QdratNew.Areas.Students.Controllers;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Migrations;
using QdratNew.Services.Exams.Implementations;
using QdratNew.Services.Exams.Models;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Students;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // معمل القياس — صف إجابة واحد لكل سؤال في المحاولة، وآخر إجابة هي المعتمدة.
    // صفوف التكرار كانت تنشأ من ضغطتين متزامنتين فتُسقط صفحة الحل بخطأ 500 دائم وتضخّم الدرجة المحفوظة.
    // بعد MSE_UniqueStudentAnswer يمنعها الفهرس الفريد؛ وتبقى القراءة متسامحة معها، ويُعطَّل الفهرس في الاختبارات التي تزرعها.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "SqlServer")]
    public sealed class MinistrySimExamDuplicateAnswerSqlServerTests : IAsyncLifetime
    {
        private const string ExamTitle = "RTKS7 تكرار إجابة معمل القياس";
        private const string PassageQuestionTitle = "RTKS7 سؤال قطعة معمل القياس";
        private const string PassageTitle = "RTKS7 قطعة معمل القياس";
        private const string UniqueIndex = "IX_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttemptId_MinistrySimExamStageQuestionId";
        private const string BackupTable = "MinistrySimExamStudentAnswers_DuplicatesBackup";

        private RtkSeed _seed = null!;
        private int _examId;
        private int _attemptId;
        private int _stageQuestionId;
        private int _secondStageQuestionId;

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;

            await CleanupExamAsync();
            _seed = await RtkRealDb.SeedAsync(students: 1, withLogin: true);

            await using var db = RtkRealDb.CreateDb();
            var courseId = await db.Batches.Where(b => b.Id == _seed.BatchId).Select(b => b.CourseId).FirstAsync();
            var instructorId = await db.Instructors.Select(i => i.Id).FirstAsync();
            var sectionId = await db.Sections.Select(s => s.Id).FirstAsync();
            var questionIds = await db.Questions
                .Where(q => q.LessonId != null && q.Options.Any())
                .Select(q => q.Id)
                .Take(2)
                .ToListAsync();

            var stage = new MinistrySimExamStage
            {
                StageNumber = 1, QuantSectionId = sectionId, QuantQuestionCount = 2,
                VerbalSectionId = sectionId, VerbalQuestionCount = 0, DurationMinutes = 26
            };
            var first = new MinistrySimExamStageQuestion { QuestionId = questionIds[0], StageOrder = 1, GlobalOrder = 1, IsQuant = true };
            var second = new MinistrySimExamStageQuestion { QuestionId = questionIds[1], StageOrder = 2, GlobalOrder = 2, IsQuant = true };
            stage.Questions.Add(first);
            stage.Questions.Add(second);

            var exam = new MinistrySimExam
            {
                CourseId = courseId, Title = ExamTitle, TotalStages = 1, IsPublished = true,
                PublishedAt = DateTime.Now, CreatedByInstructorId = instructorId
            };
            exam.Stages.Add(stage);
            db.MinistrySimExams.Add(exam);
            await db.SaveChangesAsync();

            var attempt = new MinistrySimExamStudentAttempt
            {
                MinistrySimExamId = exam.Id, StudentId = _seed.Students[0].StudentId, StartedAt = DateTime.Now
            };
            attempt.StageProgress.Add(new MinistrySimExamStudentStageProgress { StageNumber = 1, StartedAt = DateTime.Now });
            db.MinistrySimExamStudentAttempts.Add(attempt);
            await db.SaveChangesAsync();

            _examId = exam.Id;
            _attemptId = attempt.Id;
            _stageQuestionId = first.Id;
            _secondStageQuestionId = second.Id;
        }

        public async Task DisposeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;
            await CleanupExamAsync();
            await RtkRealDb.CleanupAsync();
        }

        private static async Task CleanupExamAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            var examIds = await db.MinistrySimExams.Where(e => e.Title == ExamTitle).Select(e => e.Id).ToListAsync();
            foreach (var id in examIds)
            {
                await db.MinistrySimExamAssignmentsToBatches.Where(x => x.MinistrySimExamId == id).ExecuteDeleteAsync();
                await db.MinistrySimExamStudentAnswers.Where(a => a.Attempt.MinistrySimExamId == id).ExecuteDeleteAsync();
                await db.MinistrySimExamStudentAttempts.Where(a => a.MinistrySimExamId == id).ExecuteDeleteAsync();
                await db.MinistrySimExamStageQuestions.Where(q => q.MinistrySimExamStage.MinistrySimExamId == id).ExecuteDeleteAsync();
                await db.MinistrySimExamStages.Where(s => s.MinistrySimExamId == id).ExecuteDeleteAsync();
                await db.MinistrySimExams.Where(e => e.Id == id).ExecuteDeleteAsync();
            }

            await db.Questions.Where(q => q.Title == PassageQuestionTitle).ExecuteDeleteAsync();
            await db.VerbalPassages.Where(p => p.Title == PassageTitle).ExecuteDeleteAsync();

            // يعيد تفعيل الفهرس حتى لو فشل اختبار عطّله (بعد حذف صفوف الاختبار)
            await db.Database.ExecuteSqlRawAsync($"ALTER INDEX [{UniqueIndex}] ON [MinistrySimExamStudentAnswers] REBUILD");
        }

        private static async Task DisableUniqueIndexAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            await db.Database.ExecuteSqlRawAsync($"ALTER INDEX [{UniqueIndex}] ON [MinistrySimExamStudentAnswers] DISABLE");
        }

        private async Task InsertAnswerAsync(int stageQuestionId, int? option, bool? isCorrect, bool flagged = false)
        {
            await using var db = RtkRealDb.CreateDb();
            db.MinistrySimExamStudentAnswers.Add(new MinistrySimExamStudentAnswer
            {
                MinistrySimExamStudentAttemptId = _attemptId,
                MinistrySimExamStageQuestionId = stageQuestionId,
                SelectedOptionId = option,
                IsCorrect = isCorrect,
                IsFlaggedForReview = flagged
            });
            await db.SaveChangesAsync();
        }

        private MinistrySimExamController NewController(ApplicationDbContext db)
        {
            var userManager = new Mock<UserManager<ApplicationUser>>(
                new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
            userManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(_seed.Students[0].UserId);

            return new MinistrySimExamController(
                db,
                userManager.Object,
                new MinistrySimExamAttemptService(db),
                new MinistrySimExamResultService(db),
                new Mock<IIntegrityGuardService>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        // =====================================================
        // منع التكرار
        // =====================================================
        [SqlServerFact]
        public async Task Database_RejectsSecondRowForSameQuestion()
        {
            await InsertAnswerAsync(_stageQuestionId, option: 0, isCorrect: false);

            await Assert.ThrowsAsync<DbUpdateException>(() => InsertAnswerAsync(_stageQuestionId, option: 1, isCorrect: true));
        }

        [SqlServerFact]
        public async Task ConcurrentSubmit_LeavesSingleRow_AndSolveStillWorks()
        {
            for (var i = 0; i < 25; i++)
            {
                await using var a = RtkRealDb.CreateDb();
                await using var b = RtkRealDb.CreateDb();
                await Task.WhenAll(
                    new MinistrySimExamAttemptService(a).SubmitAnswerAsync(_attemptId, _secondStageQuestionId, 0, false),
                    new MinistrySimExamAttemptService(b).SubmitAnswerAsync(_attemptId, _secondStageQuestionId, 0, false));

                await using var check = RtkRealDb.CreateDb();
                var rows = await check.MinistrySimExamStudentAnswers
                    .CountAsync(x => x.MinistrySimExamStudentAttemptId == _attemptId && x.MinistrySimExamStageQuestionId == _secondStageQuestionId);
                Assert.Equal(1, rows);

                await check.MinistrySimExamStudentAnswers
                    .Where(x => x.MinistrySimExamStudentAttemptId == _attemptId && x.MinistrySimExamStageQuestionId == _secondStageQuestionId)
                    .ExecuteDeleteAsync();
            }

            await using var db = RtkRealDb.CreateDb();
            Assert.IsType<ViewResult>(await NewController(db).Solve(_examId, stageNumber: 1));
        }

        [SqlServerFact]
        public async Task ChangingAnswer_KeepsOneRow_WithLastAnswerOnly()
        {
            await using (var db = RtkRealDb.CreateDb())
            {
                var service = new MinistrySimExamAttemptService(db);
                await service.SubmitAnswerAsync(_attemptId, _stageQuestionId, 0, false);
                await service.SubmitAnswerAsync(_attemptId, _stageQuestionId, 2, false);
                await service.SubmitAnswerAsync(_attemptId, _stageQuestionId, 1, true);
            }

            await using var verify = RtkRealDb.CreateDb();
            var row = await verify.MinistrySimExamStudentAnswers
                .SingleAsync(a => a.MinistrySimExamStudentAttemptId == _attemptId && a.MinistrySimExamStageQuestionId == _stageQuestionId);
            Assert.Equal(1, row.SelectedOptionId);
            Assert.True(row.IsFlaggedForReview);
        }

        // =====================================================
        // تنظيف ما هو موجود قبل الفهرس
        // =====================================================
        [SqlServerFact]
        public async Task DedupeScript_KeepsNewestRow_BacksUpOlder_AndDoesNotTouchScores()
        {
            await DisableUniqueIndexAsync();
            await InsertAnswerAsync(_stageQuestionId, option: 0, isCorrect: false);
            await InsertAnswerAsync(_stageQuestionId, option: 1, isCorrect: true);
            await InsertAnswerAsync(_secondStageQuestionId, option: 3, isCorrect: true);

            await using var db = RtkRealDb.CreateDb();
            await db.MinistrySimExamStudentStageProgresses
                .Where(p => p.MinistrySimExamStudentAttemptId == _attemptId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StagePercentScore, 77));
            await db.MinistrySimExamStudentAttempts
                .Where(a => a.Id == _attemptId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.TotalScorePercent, 88));

            var oldRowId = await db.MinistrySimExamStudentAnswers
                .Where(a => a.MinistrySimExamStudentAttemptId == _attemptId && a.SelectedOptionId == 0)
                .Select(a => a.Id)
                .SingleAsync();

            try
            {
                await db.Database.ExecuteSqlRawAsync(MSE_UniqueStudentAnswer.DedupeSql);

                var rows = await db.MinistrySimExamStudentAnswers
                    .Where(a => a.MinistrySimExamStudentAttemptId == _attemptId)
                    .ToListAsync();
                Assert.Equal(2, rows.Count);
                Assert.Equal(1, rows.Single(r => r.MinistrySimExamStageQuestionId == _stageQuestionId).SelectedOptionId);
                Assert.Equal(3, rows.Single(r => r.MinistrySimExamStageQuestionId == _secondStageQuestionId).SelectedOptionId);

                var backedUp = await db.Database
                    .SqlQueryRaw<int>($"SELECT COUNT(*) AS [Value] FROM [{BackupTable}] WHERE [Id] = {oldRowId}")
                    .SingleAsync();
                Assert.Equal(1, backedUp);

                // الدرجات المحفوظة لا تتغير: إعادة الحساب مؤجلة بقرار صريح
                Assert.Equal(77, await db.MinistrySimExamStudentStageProgresses.Where(p => p.MinistrySimExamStudentAttemptId == _attemptId).Select(p => p.StagePercentScore).SingleAsync());
                Assert.Equal(88, await db.MinistrySimExamStudentAttempts.Where(a => a.Id == _attemptId).Select(a => a.TotalScorePercent).SingleAsync());
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync($"DELETE FROM [{BackupTable}] WHERE [Id] = {oldRowId}");
            }
        }

        // =====================================================
        // تسامح القراءة مع صفوف مكررة (قبل تطبيق السكربت أو بعد التراجع عنه)
        // =====================================================
        [SqlServerFact]
        public async Task Solve_WithDuplicateAnswerRows_RendersUsingNewestRow()
        {
            await DisableUniqueIndexAsync();
            await InsertAnswerAsync(_stageQuestionId, option: 0, isCorrect: false);
            await InsertAnswerAsync(_stageQuestionId, option: 1, isCorrect: true, flagged: true);

            await using var db = RtkRealDb.CreateDb();
            var result = await NewController(db).Solve(_examId, stageNumber: 1);

            var vm = Assert.IsType<MinistrySimExamSolveVm>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(1, vm.SelectedOptionIndex);
            Assert.True(vm.IsFlaggedForReview);
        }

        [SqlServerFact]
        public async Task SubmitAnswer_WithDuplicateRows_UpdatesNewestRowSoStudentSeesNewAnswer()
        {
            await DisableUniqueIndexAsync();
            await InsertAnswerAsync(_stageQuestionId, option: 0, isCorrect: false);
            await InsertAnswerAsync(_stageQuestionId, option: 0, isCorrect: false);

            await using (var db = RtkRealDb.CreateDb())
            {
                await new MinistrySimExamAttemptService(db).SubmitAnswerAsync(_attemptId, _stageQuestionId, 1, flagForReview: false);
            }

            await using var verify = RtkRealDb.CreateDb();
            var newest = await verify.MinistrySimExamStudentAnswers
                .Where(a => a.MinistrySimExamStudentAttemptId == _attemptId && a.MinistrySimExamStageQuestionId == _stageQuestionId)
                .OrderByDescending(a => a.Id)
                .FirstAsync();
            Assert.Equal(1, newest.SelectedOptionId);

            var result = await NewController(verify).Solve(_examId, stageNumber: 1);
            var vm = Assert.IsType<MinistrySimExamSolveVm>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(1, vm.SelectedOptionIndex);
        }

        [SqlServerFact]
        public async Task LockStage_WithDuplicateCorrectRows_CountsQuestionOnce()
        {
            await DisableUniqueIndexAsync();
            await InsertAnswerAsync(_stageQuestionId, option: 1, isCorrect: true);
            await InsertAnswerAsync(_stageQuestionId, option: 1, isCorrect: true);

            await using var db = RtkRealDb.CreateDb();
            var progress = await new MinistrySimExamAttemptService(db).LockStageAsync(_attemptId, 1, timeExpired: true);

            // سؤال صحيح واحد من سؤالين = 50%، لا 100%
            Assert.Equal(50, progress.StagePercentScore);
            var attempt = await db.MinistrySimExamStudentAttempts.AsNoTracking().FirstAsync(a => a.Id == _attemptId);
            Assert.Equal(50, attempt.TotalScorePercent);
        }

        [SqlServerFact]
        public async Task ResultAndReview_WithDuplicateRows_UseNewestRow()
        {
            await DisableUniqueIndexAsync();
            await InsertAnswerAsync(_stageQuestionId, option: 0, isCorrect: false);
            await InsertAnswerAsync(_stageQuestionId, option: 1, isCorrect: true);
            await using (var lockDb = RtkRealDb.CreateDb())
            {
                await new MinistrySimExamAttemptService(lockDb).LockStageAsync(_attemptId, 1, timeExpired: true);
            }

            await using var db = RtkRealDb.CreateDb();
            var service = new MinistrySimExamResultService(db);
            var studentId = _seed.Students[0].StudentId;

            var result = await service.GetResultAsync(_examId, studentId);
            Assert.Equal(MinistrySimExamResultStatus.Ok, result.Status);
            Assert.Equal(1, result.Vm.TotalCorrect);
            Assert.Equal(0, result.Vm.TotalWrong);

            var review = await service.GetQuestionReviewAsync(_examId, studentId, 1);
            Assert.Equal(MinistrySimExamQuestionReviewStatus.Ok, review.Status);
        }

        [SqlServerFact]
        public async Task BatchAnalytics_WithDuplicateRows_CountsQuestionOnce()
        {
            await DisableUniqueIndexAsync();
            await InsertAnswerAsync(_stageQuestionId, option: 0, isCorrect: false);
            await InsertAnswerAsync(_stageQuestionId, option: 1, isCorrect: true);

            await using var db = RtkRealDb.CreateDb();
            db.StudentBatchEnrollments.Add(new StudentBatchEnrollment
            {
                StudentID = _seed.Students[0].StudentId, BatchId = _seed.BatchId, Status = "Active"
            });
            db.MinistrySimExamAssignmentsToBatches.Add(new MinistrySimExamAssignmentToBatch
            {
                MinistrySimExamId = _examId, BatchId = _seed.BatchId, IsOnline = true
            });
            await db.SaveChangesAsync();

            var lookup = await new MinistrySimExamBatchAnalyticsService(db, new QdratNew.Services.MinistrySim.MinistrySimOwnershipQuery(db)).GetBatchAnalyticsAsync(_examId, _seed.BatchId);

            var summary = lookup.Vm.Students.Single(s => s.StudentId == _seed.Students[0].StudentId);
            Assert.Equal(1, summary.CorrectAnswers);
            Assert.Equal(0, summary.WrongAnswers);
            Assert.Equal(1, summary.SkippedAnswers);
        }

        // =====================================================
        // عرض القطعة اللفظية والمحور في شاشة الحل
        // =====================================================
        [SqlServerFact]
        public async Task Solve_VerbalQuestion_CarriesPassageAndAxisToView()
        {
            await using var db = RtkRealDb.CreateDb();
            var template = await db.Questions
                .Where(q => q.Lesson.Section != null && q.Options.Any())
                .Select(q => new { q.CurriculumId, q.LessonId, SectionTitle = q.Lesson.Section.Title })
                .FirstAsync();

            var passage = new VerbalPassage { Title = PassageTitle, Content = "نص القطعة للاختبار", CreatedAt = DateTime.Now, Type = PassageType.Text };
            var question = new Question
            {
                Title = PassageQuestionTitle, CorrectAnswer = "أ", ReferenceNumber = Guid.NewGuid().ToString("N")[..12],
                CurriculumId = template.CurriculumId, LessonId = template.LessonId, VerbalPassage = passage
            };
            db.Questions.Add(question);
            await db.SaveChangesAsync();

            await db.MinistrySimExamStageQuestions
                .Where(sq => sq.Id == _stageQuestionId)
                .ExecuteUpdateAsync(s => s.SetProperty(sq => sq.QuestionId, question.Id));

            var result = await NewController(db).Solve(_examId, stageNumber: 1, q: question.Id);

            var vm = Assert.IsType<MinistrySimExamSolveVm>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal("نص القطعة للاختبار", vm.Question.VerbalPassageContent);
            Assert.Equal(PassageTitle, vm.Question.VerbalPassageTitle);
            Assert.Equal(PassageType.Text, vm.Question.VerbalPassageType);
            Assert.Equal(template.SectionTitle, vm.Question.SectionTitle);
        }
    }
}
