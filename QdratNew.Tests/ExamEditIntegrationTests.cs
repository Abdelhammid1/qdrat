using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using QdratNew.Areas.Admin.Controllers;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Exams.Implementations;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;
using System.Security.Claims;
using Xunit;

namespace QdratNew.Tests
{
    // Sprint 4 (EE1) — اختبار تكامل: تعديل أسئلة اختبار فردي له محاولات إجابة سابقة، يتأكد
    // أن حراسة EB2 (Sprint 2) تمنع الحفظ بدون تأكيد+سبب، وتنظّف صح عند التأكيد.
    public class ExamEditIntegrationTests
    {
        private static ExamIndividualAssignmentsController BuildController(ApplicationDbContext db)
        {
            var controller = new ExamIndividualAssignmentsController(
                db,
                Mock.Of<IExamRecommendationService>(),
                Mock.Of<QdratNew.Services.Exams.Abstractions.IExamResultEngine>(),
                new ExamAssignmentIntegrityService(db),
                Mock.Of<IAdminActivityLogger>());

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            return controller;
        }

        private static async Task<(Exam exam, ExamAssignmentToStudent assignment, List<Question> newQuestions)>
            SeedAsync(ApplicationDbContext db)
        {
            var exam = new Exam { Title = "اختبار قبل التعديل", CurriculumId = null, TotalQuestions = 10 };
            db.Exams.Add(exam);

            var oldQuestion = new Question
            {
                Id = Guid.NewGuid(),
                Title = "سؤال قديم",
                ReferenceNumber = "Q-OLD",
                CurriculumId = 1,
                LessonId = 1
            };
            db.Questions.Add(oldQuestion);
            await db.SaveChangesAsync();

            var assignment = new ExamAssignmentToStudent
            {
                ExamId = exam.Id,
                StudentId = 100,
                ScheduledDate = DateTime.UtcNow.AddHours(-2),
                EndAt = DateTime.UtcNow.AddHours(-1),
                DurationMinutes = 30
            };
            db.ExamAssignmentsToStudents.Add(assignment);
            await db.SaveChangesAsync();

            db.ExamQuestions.Add(new ExamQuestion
            {
                QuestionId = oldQuestion.Id,
                ExamId = exam.Id,
                ExamAssignmentToStudentId = assignment.Id,
                Order = 1
            });

            // محاولة إجابة مسجّلة بالفعل على السؤال القديم — التسليم تم
            db.QuestionAttemptNew.Add(new QuestionAttemptNew
            {
                StudentId = 100,
                QuestionId = oldQuestion.Id,
                ExamAssignmentToStudentId = assignment.Id,
                IsCorrect = true,
                SelectedAnswer = "A",
                AttemptedAt = DateTime.UtcNow
            });

            db.ExamStudentStatuses.Add(new ExamStudentStatus
            {
                StudentId = 100,
                ExamId = exam.Id,
                ExamAssignmentToStudentId = assignment.Id,
                IsSubmitted = true,
                Status = QdratNew.Enums.ExamStatus.Completed
            });

            // النموذج الاحترافي البديل المطلوب اختياره بعد التأكيد
            var newQuestion = new Question
            {
                Id = Guid.NewGuid(),
                Title = "سؤال جديد",
                ReferenceNumber = "Q-NEW",
                CurriculumId = 1,
                LessonId = 1
            };
            db.Questions.Add(newQuestion);

            var model = new ProfessionalModel { Title = "M-TEST", Description = "نموذج اختبار", CreatedBy = "Test" };
            db.ProfessionalModels.Add(model);
            await db.SaveChangesAsync();

            db.ProfessionalModelQuestions.Add(new ProfessionalModelQuestion
            {
                ModelId = model.Id,
                QuestionId = newQuestion.Id,
                OrderNumber = 1
            });
            await db.SaveChangesAsync();

            return (exam, assignment, new List<Question> { newQuestion });
        }

        private static EditStudentExamVm BuildVm(Exam exam, ExamAssignmentToStudent assignment, int professionalModelId)
            => new EditStudentExamVm
            {
                AssignmentId = assignment.Id,
                ExamId = exam.Id,
                Title = "اختبار بعد التعديل",
                StudentId = assignment.StudentId,
                UseProfessionalModel = true,
                UseAutoGeneration = false,
                ProfessionalModelId = professionalModelId,
                ScheduledDate = assignment.ScheduledDate!.Value,
                EndAt = assignment.EndAt!.Value,
                DurationMinutes = assignment.DurationMinutes,
                IsOnline = true
            };

        [Fact]
        public async Task EditStudentExam_WithPriorAttempts_WithoutConfirm_RejectsAndKeepsOldQuestions()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var db = new ApplicationDbContext(options);
            var (exam, assignment, _) = await SeedAsync(db);

            var model = await db.ProfessionalModels.FirstAsync();
            var vm = BuildVm(exam, assignment, model.Id);
            vm.ConfirmResetAttempts = false; // بدون تأكيد

            var controller = BuildController(db);
            await controller.EditStudentExam(vm);

            // مفيش أسئلة اتغيرت، ومفيش محاولات انمسحت
            var remainingQuestions = await db.ExamQuestions
                .Where(q => q.ExamAssignmentToStudentId == assignment.Id)
                .ToListAsync();
            Assert.Single(remainingQuestions);
            Assert.Equal("Q-OLD", (await db.Questions.FindAsync(remainingQuestions[0].QuestionId))!.ReferenceNumber);

            var attemptsStillThere = await db.QuestionAttemptNew
                .CountAsync(a => a.ExamAssignmentToStudentId == assignment.Id);
            Assert.Equal(1, attemptsStillThere);
        }

        [Fact]
        public async Task EditStudentExam_WithPriorAttempts_ConfirmedWithReason_ResetsAndReplacesQuestions()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var db = new ApplicationDbContext(options);
            var (exam, assignment, newQuestions) = await SeedAsync(db);

            var model = await db.ProfessionalModels.FirstAsync();
            var vm = BuildVm(exam, assignment, model.Id);
            vm.ConfirmResetAttempts = true;
            vm.EditReason = "تصحيح خطأ في اختيار الأسئلة";

            var controller = BuildController(db);
            await controller.EditStudentExam(vm);

            var remainingQuestions = await db.ExamQuestions
                .Where(q => q.ExamAssignmentToStudentId == assignment.Id)
                .ToListAsync();
            Assert.Single(remainingQuestions);
            Assert.Equal(newQuestions[0].Id, remainingQuestions[0].QuestionId);

            var attemptsAfter = await db.QuestionAttemptNew
                .CountAsync(a => a.ExamAssignmentToStudentId == assignment.Id);
            Assert.Equal(0, attemptsAfter);

            var status = await db.ExamStudentStatuses.FirstAsync(s => s.ExamAssignmentToStudentId == assignment.Id);
            Assert.False(status.IsSubmitted);
            Assert.Null(status.Note);
        }

        [Fact]
        public async Task EditStudentExam_WithPriorAttempts_ConfirmedWithoutReason_RejectsWithError()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var db = new ApplicationDbContext(options);
            var (exam, assignment, _) = await SeedAsync(db);

            var model = await db.ProfessionalModels.FirstAsync();
            var vm = BuildVm(exam, assignment, model.Id);
            vm.ConfirmResetAttempts = true;
            vm.EditReason = null; // سبب مفقود

            var controller = BuildController(db);
            await controller.EditStudentExam(vm);

            var attemptsStillThere = await db.QuestionAttemptNew
                .CountAsync(a => a.ExamAssignmentToStudentId == assignment.Id);
            Assert.Equal(1, attemptsStillThere); // لم يُنظَّف شيء
        }
    }
}
