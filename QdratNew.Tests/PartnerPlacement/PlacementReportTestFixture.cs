using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PuppeteerSharp;
using QdratNew.Areas.Admin.Controllers;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services;
using QdratNew.Services.Exams.Placement;
using QdratNew.Services.Implementations;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Reports;
using Xunit;

namespace QdratNew.Tests.PartnerPlacement
{
    // PPL-S3.3 — بيانات ثابتة لتقارير تحديد المستوى، تعمل على InMemory وعلى SQL Server.
    internal sealed record PlacementReportSeed
    {
        public int InstituteExam { get; init; }
        public int InstituteExam2 { get; init; }
        public int InstituteExamOnPartnerBatch { get; init; }
        public int ExamOfA { get; init; }
        public int ExamOfB { get; init; }
        public int InstituteBatch { get; init; }
        public int BatchA { get; init; }
        public int BatchB { get; init; }
        public int PartnerA { get; init; }
        public int PartnerB { get; init; }

        public int FullStudent { get; init; }           // أجاب كل الأسئلة وسلّم
        public int PartialStudent { get; init; }        // أجاب سؤالًا واحدًا وموقوف بـ Translation Guard
        public int OtherExamStudent { get; init; }      // حلّ InstituteExam2 فقط في نفس الدفعة
        public int NoAssignmentStudent { get; init; }   // له إجابات بلا سجل تعيين
        public int ActiveA { get; init; }
        public int ArchivedA { get; init; }
        public int OnlyPartnerExamA { get; init; }      // في دفعة A وحلّ اختبار الشريك فقط
        public int ForeignInBatchA { get; init; }       // طالب الشريك B مُسجَّل خطأً في دفعة A
        public int StudentB { get; init; }

        public int FullAssignment { get; init; }
        public int PartialAssignment { get; init; }
        public int OtherExamAssignment { get; init; }
        public int ActiveAAssignment { get; init; }
        public int ArchivedAAssignment { get; init; }
        public int ActiveAInstituteAssignment { get; init; }
        public int StudentBAssignment { get; init; }

        public int Lesson1 { get; init; }
        public int Lesson2 { get; init; }
        public int Lesson3 { get; init; }
        public int LessonNotInExams { get; init; }
    }

    internal static class PlacementReportFixture
    {
        public static readonly Guid Q1 = Guid.Parse("11111111-3333-4000-8000-000000000001");
        public static readonly Guid Q2 = Guid.Parse("11111111-3333-4000-8000-000000000002");
        public static readonly Guid Q3 = Guid.Parse("11111111-3333-4000-8000-000000000003");
        public static readonly Guid Q4 = Guid.Parse("11111111-3333-4000-8000-000000000004");

        public const string Marker = "PPLS3";
        private static readonly DateTime Base = new(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc);

        public static async Task<PlacementReportSeed> SeedAsync(ApplicationDbContext db)
        {
            var partnerA = new Partner { Name = $"{Marker} A", Code = $"{Marker}-A", PartnershipStart = Base, PartnershipEnd = Base.AddYears(5) };
            var partnerB = new Partner { Name = $"{Marker} B", Code = $"{Marker}-B", PartnershipStart = Base, PartnershipEnd = Base.AddYears(5) };
            db.Partners.AddRange(partnerA, partnerB);
            await db.SaveChangesAsync();

            var branchInstitute = NewBranch("معهد", null);
            var branchA = NewBranch("A", partnerA.Id);
            var branchB = NewBranch("B", partnerB.Id);
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = Base, IsActive = true };
            db.Branches.AddRange(branchInstitute, branchA, branchB);
            db.Courses.Add(course);
            await db.SaveChangesAsync();

            var batchInstitute = NewBatch("دفعة المعهد", branchInstitute.Id, course.Id);
            var batchA = NewBatch("دفعة A", branchA.Id, course.Id);
            var batchB = NewBatch("دفعة B", branchB.Id, course.Id);
            db.Batches.AddRange(batchInstitute, batchA, batchB);
            await db.SaveChangesAsync();

            // ===== المنهج والأسئلة =====
            var quant = new Curriculum { Title = $"{Marker} منهج كمي", Description = "-", CurriculumTypeName = "-" };
            var verbal = new Curriculum { Title = $"{Marker} منهج لفظي", Description = "-", CurriculumTypeName = "-" };
            db.Curriculums.AddRange(quant, verbal);
            await db.SaveChangesAsync();

            var sectionQuant = new Section { Title = $"{Marker} محور الجبر", CurriculumId = quant.Id };
            var sectionVerbal = new Section { Title = $"{Marker} محور الاستيعاب", CurriculumId = verbal.Id };
            db.Sections.AddRange(sectionQuant, sectionVerbal);
            await db.SaveChangesAsync();

            var unit = new Unit { Title = $"{Marker} وحدة" };
            db.Units.Add(unit);
            await db.SaveChangesAsync();

            var lesson1 = NewLesson("مؤشر المعادلات", sectionQuant.Id, unit.Id);
            var lesson2 = NewLesson("مؤشر النسب", sectionQuant.Id, unit.Id);
            var lesson3 = NewLesson("مؤشر الفهم", sectionVerbal.Id, unit.Id);
            var lessonNotInExams = NewLesson("مؤشر خارج الاختبارات", sectionVerbal.Id, unit.Id);
            db.Lessons.AddRange(lesson1, lesson2, lesson3, lessonNotInExams);
            await db.SaveChangesAsync();

            var passage = new VerbalPassage { Title = $"{Marker} قطعة", Content = "نص القطعة", CreatedAt = Base };
            db.VerbalPassages.Add(passage);
            await db.SaveChangesAsync();

            db.Questions.AddRange(
                NewQuestion(Q1, 1, quant.Id, sectionQuant.Id, lesson1.Id, quantitative: true),
                NewQuestion(Q2, 2, quant.Id, sectionQuant.Id, lesson1.Id, quantitative: true),
                NewQuestion(Q3, 3, quant.Id, sectionQuant.Id, lesson2.Id, quantitative: true),
                NewQuestion(Q4, 4, verbal.Id, sectionVerbal.Id, lesson3.Id, quantitative: false, passageId: passage.Id));
            await db.SaveChangesAsync();

            // حفظ كل خيار وحده: ترتيب الإدخال = ترتيب الـ Id، كما تعيدها SQL Server عبر فهرس QuestionId
            foreach (var questionId in new[] { Q1, Q2, Q3, Q4 })
            {
                foreach (var text in new[] { "الخيار أ", "الخيار ب" })
                {
                    db.QuestionOptions.Add(new QuestionOption { QuestionId = questionId, Text = text });
                    await db.SaveChangesAsync();
                }
            }

            // ===== ولي الأمر والطلاب =====
            var parentUser = NewUser("parent");
            db.Users.Add(parentUser);
            var parent = new Parent { NationalID = $"{Marker}P", FullName = "ولي أمر", Email = "p@test.local", PhoneNumber = "0500000000", UserId = parentUser.Id };
            db.Parents.Add(parent);
            await db.SaveChangesAsync();

            var full = NewStudent(db, "full", branchInstitute.Id, parent.ParentID);
            var partial = NewStudent(db, "partial", branchInstitute.Id);
            var otherExam = NewStudent(db, "other", branchInstitute.Id);
            var noAssignment = NewStudent(db, "noassign", branchInstitute.Id);
            var activeA = NewStudent(db, "activea", branchA.Id);
            var archivedA = NewStudent(db, "archiveda", branchA.Id, active: false);
            var onlyPartner = NewStudent(db, "onlypartner", branchA.Id);
            var foreign = NewStudent(db, "foreign", branchB.Id);
            var studentB = NewStudent(db, "studentb", branchB.Id);
            await db.SaveChangesAsync();

            db.StudentBatchEnrollments.AddRange(
                Enroll(full, batchInstitute), Enroll(partial, batchInstitute), Enroll(otherExam, batchInstitute), Enroll(noAssignment, batchInstitute),
                Enroll(activeA, batchA), Enroll(archivedA, batchA), Enroll(onlyPartner, batchA), Enroll(foreign, batchA),
                Enroll(studentB, batchB));

            // ===== الاختبارات =====
            var instituteExam = NewExam("المعهد 1", null);
            var instituteExam2 = NewExam("المعهد 2", null);
            var instituteOnA = NewExam("المعهد على دفعة A", null);
            var examOfA = NewExam("الشريك A", partnerA.Id);
            var examOfB = NewExam("الشريك B", partnerB.Id);
            db.Exams.AddRange(instituteExam, instituteExam2, instituteOnA, examOfA, examOfB);
            await db.SaveChangesAsync();

            db.ExamAssignmentsToBatches.AddRange(
                NewBatchAssignment(instituteExam, batchInstitute),
                NewBatchAssignment(instituteExam2, batchInstitute),
                NewBatchAssignment(instituteOnA, batchA),
                NewBatchAssignment(examOfA, batchA),
                NewBatchAssignment(examOfB, batchB));

            foreach (var exam in new[] { instituteExam, instituteExam2, instituteOnA, examOfA, examOfB })
            {
                db.ExamQuestions.AddRange(
                    new ExamQuestion { ExamId = exam.Id, QuestionId = Q1, Order = 1 },
                    new ExamQuestion { ExamId = exam.Id, QuestionId = Q2, Order = 2 },
                    new ExamQuestion { ExamId = exam.Id, QuestionId = Q3, Order = 3 },
                    new ExamQuestion { ExamId = exam.Id, QuestionId = Q4, Order = 4 });
            }
            await db.SaveChangesAsync();

            // ===== التعيينات =====
            var aFull = NewAssignment(instituteExam, full);
            var aPartial = NewAssignment(instituteExam, partial);
            var aOther = NewAssignment(instituteExam2, otherExam);
            var aActiveA = NewAssignment(examOfA, activeA);
            var aArchivedA = NewAssignment(examOfA, archivedA);
            var aOnlyPartner = NewAssignment(examOfA, onlyPartner);
            var aForeign = NewAssignment(examOfA, foreign);
            var aActiveAInstitute = NewAssignment(instituteOnA, activeA);
            var aStudentB = NewAssignment(examOfB, studentB);
            db.ExamAssignments.AddRange(aFull, aPartial, aOther, aActiveA, aArchivedA, aOnlyPartner, aForeign, aActiveAInstitute, aStudentB);
            await db.SaveChangesAsync();

            // ===== الحالات والمحاولات =====
            db.ExamStudentStatuses.AddRange(
                new ExamStudentStatus { StudentId = full.StudentID, ExamId = instituteExam.Id, AssignedAt = Base, StartedAt = Base, SubmittedAt = Base.AddMinutes(20), IsSubmitted = true, Status = ExamStatus.Completed },
                new ExamStudentStatus { StudentId = partial.StudentID, ExamId = instituteExam.Id, AssignedAt = Base, StartedAt = Base, Status = ExamStatus.InProgress },
                new ExamStudentStatus { StudentId = activeA.StudentID, ExamId = examOfA.Id, AssignedAt = Base, StartedAt = Base, SubmittedAt = Base.AddMinutes(10), IsSubmitted = true, Status = ExamStatus.Completed });

            db.QuestionAttemptNew.AddRange(
                // المعهد 1: طالب كامل (2 صحيح، خطأ، لا أعرف)
                NewAttempt(full, instituteExam, aFull, Q1, true, 30),
                NewAttempt(full, instituteExam, aFull, Q2, false, 40, "الخيار ب"),
                NewAttempt(full, instituteExam, aFull, Q3, false, 50, "لا أعرف", dontKnow: true),
                NewAttempt(full, instituteExam, aFull, Q4, true, 60),
                // المعهد 1: طالب جزئي + طالب بلا تعيين
                NewAttempt(partial, instituteExam, aPartial, Q1, false, 45, "الخيار ب"),
                NewAttempt(noAssignment, instituteExam, null, Q1, true, 25),
                // المعهد 2: يظهر عبر الـ fallback في صفحة المعهد 1
                NewAttempt(otherExam, instituteExam2, aOther, Q1, true, 20),
                NewAttempt(otherExam, instituteExam2, aOther, Q2, false, 20, "الخيار ب"),
                // اختبار الشريك A
                NewAttempt(activeA, examOfA, aActiveA, Q1, true, 30),
                NewAttempt(activeA, examOfA, aActiveA, Q2, true, 30),
                NewAttempt(archivedA, examOfA, aArchivedA, Q1, false, 30, "الخيار ب"),
                NewAttempt(onlyPartner, examOfA, aOnlyPartner, Q1, true, 30),
                NewAttempt(foreign, examOfA, aForeign, Q1, true, 30),
                // اختبار المعهد على دفعة A
                NewAttempt(activeA, instituteOnA, aActiveAInstitute, Q1, false, 30, "الخيار ب"),
                // اختبار الشريك B
                NewAttempt(studentB, examOfB, aStudentB, Q1, true, 30));

            db.IntegrityViolationLogs.Add(new IntegrityViolationLog
            {
                StudentId = partial.StudentID,
                AttemptType = IntegrityAttemptType.Placement,
                AttemptEntityId = aPartial.Id,
                DetectedAt = Base
            });

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            return new PlacementReportSeed
            {
                InstituteExam = instituteExam.Id,
                InstituteExam2 = instituteExam2.Id,
                InstituteExamOnPartnerBatch = instituteOnA.Id,
                ExamOfA = examOfA.Id,
                ExamOfB = examOfB.Id,
                InstituteBatch = batchInstitute.Id,
                BatchA = batchA.Id,
                BatchB = batchB.Id,
                PartnerA = partnerA.Id,
                PartnerB = partnerB.Id,
                FullStudent = full.StudentID,
                PartialStudent = partial.StudentID,
                OtherExamStudent = otherExam.StudentID,
                NoAssignmentStudent = noAssignment.StudentID,
                ActiveA = activeA.StudentID,
                ArchivedA = archivedA.StudentID,
                OnlyPartnerExamA = onlyPartner.StudentID,
                ForeignInBatchA = foreign.StudentID,
                StudentB = studentB.StudentID,
                FullAssignment = aFull.Id,
                PartialAssignment = aPartial.Id,
                OtherExamAssignment = aOther.Id,
                ActiveAAssignment = aActiveA.Id,
                ArchivedAAssignment = aArchivedA.Id,
                ActiveAInstituteAssignment = aActiveAInstitute.Id,
                StudentBAssignment = aStudentB.Id,
                Lesson1 = lesson1.Id,
                Lesson2 = lesson2.Id,
                Lesson3 = lesson3.Id,
                LessonNotInExams = lessonNotInExams.Id
            };
        }

        private static Branch NewBranch(string name, int? partnerId)
            => new() { Name = $"{Marker} {name}", PartnerId = partnerId, Location = "-", City = "-", State = "-", Country = "-" };

        private static Batch NewBatch(string name, int branchId, int courseId)
            => new() { Name = $"{Marker} {name}", BranchId = branchId, CourseId = courseId, StartDate = Base, IsActive = true };

        private static Lesson NewLesson(string title, int sectionId, int unitId)
            => new() { Title = $"{Marker} {title}", Content = "-", SectionId = sectionId, UnitId = unitId };

        private static Question NewQuestion(Guid id, int number, int curriculumId, int sectionId, int lessonId, bool quantitative, int? passageId = null) => new()
        {
            Id = id,
            Title = $"{Marker} سؤال {number}",
            ReferenceNumber = $"{Marker}-{number}",
            CorrectAnswer = "الخيار أ",
            CurriculumId = curriculumId,
            SectionId = sectionId,
            LessonId = lessonId,
            IsQuantitative = quantitative,
            VerbalPassageId = passageId,
            IsComplete = true,
            IsReviewed = true,
            CreatedAt = Base
        };

        private static ApplicationUser NewUser(string key)
        {
            var userName = $"ppls3-{key}";
            return new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = $"{userName}@test.local",
                NormalizedEmail = $"{userName}@test.local".ToUpperInvariant(),
                NationalID = $"{Marker}{key[..Math.Min(5, key.Length)]}",
                FullName = key,
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
        }

        private static Student NewStudent(ApplicationDbContext db, string key, int branchId, int? parentId = null, bool active = true)
        {
            var user = NewUser(key);
            db.Users.Add(user);
            var student = new Student
            {
                NationalID = $"{Marker}{key[..Math.Min(5, key.Length)]}",
                FullName = $"طالب {key}",
                Gender = "ذكر",
                Age = 17,
                School = "-",
                Level = "-",
                UserId = user.Id,
                BranchId = branchId,
                ParentId = parentId,
                IsActiveForLearning = active
            };
            db.Students.Add(student);
            return student;
        }

        private static StudentBatchEnrollment Enroll(Student student, Batch batch)
            => new() { StudentID = student.StudentID, BatchId = batch.Id };

        private static Exam NewExam(string title, int? ownerPartnerId) => new()
        {
            Title = $"{Marker} {title}",
            Type = ExamType.LevelAssessment,
            DurationMinutes = 30,
            TotalQuestions = 4,
            OwnerPartnerId = ownerPartnerId,
            CreatedAt = Base
        };

        private static ExamAssignmentToBatch NewBatchAssignment(Exam exam, Batch batch)
            => new() { ExamId = exam.Id, BatchId = batch.Id, Title = "اختبار تحديد المستوى", IsSentToStudents = true, CreatedAt = Base };

        private static ExamAssignment NewAssignment(Exam exam, Student student)
            => new() { ExamId = exam.Id, StudentId = student.StudentID, AssignedAt = Base };

        private static QuestionAttemptNew NewAttempt(Student student, Exam exam, ExamAssignment? assignment, Guid questionId, bool correct, double seconds, string answer = "الخيار أ", bool dontKnow = false) => new()
        {
            StudentId = student.StudentID,
            ExamId = exam.Id,
            ExamAssignmentId = assignment?.Id,
            QuestionId = questionId,
            IsCorrect = correct,
            SelectedAnswer = answer,
            IsDontKnowAnswer = dontKnow,
            TimeTakenSeconds = seconds,
            AttemptedAt = Base
        };
    }

    internal static class PlacementReportHarness
    {
        // ActivatorUtilities حتى يستمر نفس الاختبار قبل نقل المنطق وبعده رغم تغيّر الـ Constructor.
        public static PlacementExamsController NewAdminController(ApplicationDbContext db)
        {
            var timeZone = new Mock<ITimeZoneService>();
            timeZone.Setup(x => x.GetNowSaudi()).Returns(new DateTime(2026, 1, 11, 12, 0, 0));
            timeZone.Setup(x => x.GetNowUtc()).Returns(new DateTime(2026, 1, 11, 9, 0, 0));

            var services = new ServiceCollection();
            services.AddSingleton(db);
            services.AddSingleton(new Mock<IPlacementExamManagementService>().Object);
            services.AddSingleton(new Mock<QdratNew.Services.PartnerDispatch.IDispatchAudienceService>().Object); // APD-S3.1
            services.AddSingleton(new Mock<QdratNew.Services.PartnerDispatch.IDispatchAudiencePickerBuilder>().Object);
            services.AddSingleton(new Mock<QdratNew.Services.PartnerDispatch.IPartnerPlacementDispatchService>().Object);
            services.AddSingleton(timeZone.Object);
            services.AddSingleton<IExamRecommendationService, ExamRecommendationService>();
            services.AddSingleton(new Mock<UserManager<ApplicationUser>>(
                new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!).Object);
            services.AddSingleton(new Mock<IAdminActivityLogger>().Object);
            services.AddSingleton(new ReportBrowserProvider(new Mock<IBrowserFetcher>().Object));
            services.AddSingleton<IPlacementExamScopeGuard, PlacementExamScopeGuard>();
            services.AddSingleton(new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>().Object);
            services.AddLogging();
            services.AddSingleton<IPlacementExamReportService, PlacementExamReportService>();

            var controller = ActivatorUtilities.CreateInstance<PlacementExamsController>(services.BuildServiceProvider());
            var httpContext = new DefaultHttpContext();
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, new Mock<ITempDataProvider>().Object);
            return controller;
        }

        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        /// <summary>تمثيل نصي لكل ما يؤثر في الاستجابة: نوعها، اسم الـ View، الموديل، ViewBag، TempData.</summary>
        public static string Describe(PlacementExamsController controller, IActionResult result)
        {
            var root = new JsonObject { ["result"] = result.GetType().Name };

            switch (result)
            {
                case ViewResult view:
                    root["viewName"] = view.ViewName;
                    root["model"] = view.Model == null ? null : JsonSerializer.SerializeToNode(view.Model, view.Model.GetType(), Json);
                    var viewBag = new JsonObject();
                    foreach (var pair in view.ViewData.OrderBy(p => p.Key))
                        viewBag[pair.Key] = pair.Value?.ToString();
                    root["viewBag"] = viewBag;
                    break;
                case ContentResult content:
                    root["content"] = content.Content;
                    root["contentType"] = content.ContentType;
                    break;
                case ObjectResult obj:
                    root["statusCode"] = obj.StatusCode;
                    root["value"] = obj.Value?.ToString();
                    break;
                case RedirectToActionResult redirect:
                    root["action"] = redirect.ActionName;
                    var routeValues = new JsonObject();
                    foreach (var pair in redirect.RouteValues ?? new Microsoft.AspNetCore.Routing.RouteValueDictionary())
                        routeValues[pair.Key] = pair.Value?.ToString();
                    root["route"] = routeValues;
                    break;
                case FileContentResult file:
                    root["fileName"] = file.FileDownloadName;
                    root["contentType"] = file.ContentType;
                    root["isPdf"] = file.FileContents.Length > 4 && file.FileContents[0] == '%' && file.FileContents[1] == 'P';
                    break;
            }

            var tempData = new JsonObject();
            foreach (var pair in controller.TempData.OrderBy(p => p.Key))
                tempData[pair.Key] = pair.Value?.ToString();
            root["tempData"] = tempData;

            return root.ToJsonString(Json);
        }

        /// <summary>PPL_SNAPSHOT_WRITE=1 يسجّل الـ Snapshot؛ سُجّلت من Controller الأدمن قبل نقل المنطق للخدمة.</summary>
        public static void VerifySnapshot(string name, string actual, [CallerFilePath] string callerPath = "")
        {
            var path = Path.Combine(Path.GetDirectoryName(callerPath)!, "Snapshots", $"{name}.json");

            if (Environment.GetEnvironmentVariable("PPL_SNAPSHOT_WRITE") == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, actual);
                return;
            }

            Assert.True(File.Exists(path), $"Snapshot غير موجود: {path}");
            Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual.ReplaceLineEndings("\n"));
        }
    }
}
