using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Tests.AdminPartnerDispatch
{
    // APD-S1 — بيانات اختبار مشتركة على قاعدة الاختبار الحقيقية (العلامة APDS1)
    //
    // A: عقد نشط (واجبات، اختبارات، مؤشر أداء، نماذج) — بلا تحديد مستوى ولا معمل قياس. النموذج M1 مربوط به.
    // B: عقد نشط بلا واجبات، مع معمل القياس. C: شراكة سارية بلا عقد. D: شراكة منتهية بلا عقد (لا يُعرض).
    // دفعات A: A1 (طالب نشط واحد + غير نشط + طالب شريك آخر + منسحب)، Aempty، A2 مؤرشفة، A3 بمنهج آخر.
    internal sealed record ApdSeed(
        int PartnerA, int PartnerB, int PartnerC, int PartnerD,
        int CurriculumId, int OtherCurriculumId, int CourseId, int OtherCourseId,
        int BatchA1, int BatchAEmpty, int BatchA2Archived, int BatchA3OtherCurriculum,
        int BatchB1, int BatchC1, int BatchI1, int BatchI2OtherCourse,
        int ActiveStudentA, int StudentI1,
        int ModelLinkedToA, int ModelNotForAnyone, int LessonId, Guid QuestionId);

    // APD-S3: نموذج اختبار متاح لـ A وB بعقدين يشملان تحديد المستوى، ودفعة ثانية لـ A فيها طالب نشط.
    internal sealed record ApdPlacementSeed(int ModelId, int BatchA4, int StudentA4);

    // APD-S4: منهج مستقل بمحور ودرس وسؤالين مرتبط بكورس الدفعات، وقالب جاهز، وعقدا A وB يشملان مؤشر الأداء.
    internal sealed record ApdPerformanceSeed(int CurriculumId, int TemplateId, int SectionId, Guid QuestionA, Guid QuestionB, int StudentB);

    internal static class ApdTestData
    {
        public const string Marker = "APDS1";
        public const string SenderId = "apds1-sender";

        private const string Partners = "(SELECT Id FROM Partners WHERE Code LIKE 'APDS1%')";
        private const string Batches = "(SELECT Id FROM Batches WHERE Name LIKE 'APDS1%')";
        private const string Sets = "(SELECT Id FROM HomeworkSets WHERE BatchId IN " + Batches + ")";
        private const string Courses = "(SELECT Id FROM Courses WHERE Name LIKE 'APDS1%')";
        private const string Profiles = "(SELECT Id FROM AdminProfiles WHERE Name LIKE 'APDS1%')";
        private const string Students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'APDS1%')";
        private const string Assignments = "(SELECT Id FROM ExamAssignmentsToBatches WHERE BatchId IN " + Batches + ")";
        private const string Models = "(SELECT Id FROM ProfessionalModels WHERE Title LIKE 'APDS1%')";
        private const string PerformanceExams = "(SELECT Id FROM PerformanceIndicatorExams WHERE Title LIKE 'APDS1%' OR OwnerPartnerId IN " + Partners + ")";

        public static async Task CleanupAsync(ApplicationDbContext db)
        {
            foreach (var sql in new[]
            {
                $"DELETE FROM AdminUserProfiles WHERE AdminProfileId IN {Profiles}",
                $"DELETE FROM AdminProfileCustomPermissions WHERE AdminProfileControllerPermissionId IN (SELECT Id FROM AdminProfileControllerPermissions WHERE AdminProfileId IN {Profiles})",
                $"DELETE FROM AdminProfileControllerPermissions WHERE AdminProfileId IN {Profiles}",
                "DELETE FROM AdminProfiles WHERE Name LIKE 'APDS1%'",
                "DELETE FROM AdminUserProfiles WHERE UserId LIKE 'apds1-%'",
                $"DELETE FROM PartnerDispatchLogs WHERE PartnerId IN {Partners}",
                $"DELETE FROM QuestionAttemptNew WHERE PerformanceIndicatorExamId IN {PerformanceExams}",
                $"DELETE FROM PerformanceIndicatorExamStudents WHERE PerformanceIndicatorExamId IN {PerformanceExams}",
                $"DELETE FROM PerformanceIndicatorExamQuestions WHERE PerformanceIndicatorExamId IN {PerformanceExams}",
                $"DELETE FROM PerformanceIndicatorExamSections WHERE PerformanceIndicatorExamId IN {PerformanceExams}",
                $"DELETE FROM PerformanceIndicatorExamToBatch WHERE PerformanceIndicatorExamId IN {PerformanceExams}",
                "DELETE FROM PerformanceIndicatorExams WHERE Title LIKE 'APDS1%' OR OwnerPartnerId IN " + Partners,
                $"DELETE FROM UserPartners WHERE PartnerId IN {Partners}",
                $"DELETE FROM Notifications WHERE StudentID IN {Students}",
                "DELETE FROM SystemLogs WHERE Description LIKE N'%APDS1%'",
                $"DELETE FROM ExamStudentStatuses WHERE StudentId IN {Students}",
                $"DELETE FROM ExamAssignments WHERE StudentId IN {Students}",
                $"DELETE FROM ExamQuestions WHERE ExamAssignmentId IN {Assignments}",
                $"DELETE FROM ExamQuestions WHERE ExamId IN (SELECT Id FROM Exams WHERE SourceProfessionalModelId IN {Models} OR OwnerPartnerId IN {Partners})",
                $"DELETE FROM ExamAssignmentsToBatches WHERE BatchId IN {Batches}",
                $"DELETE FROM Exams WHERE SourceProfessionalModelId IN {Models} OR OwnerPartnerId IN {Partners}",
                $"DELETE FROM Homeworks WHERE HomeworkSetId IN {Sets}",
                $"DELETE FROM HomeworkSetStudents WHERE HomeworkSetId IN {Sets}",
                $"DELETE FROM HomeworkSets WHERE BatchId IN {Batches}",
                $"DELETE FROM HomeworkDraftQuestions WHERE HomeworkDraftId IN (SELECT Id FROM HomeworkDrafts WHERE PartnerId IN {Partners})",
                $"DELETE FROM HomeworkDrafts WHERE PartnerId IN {Partners}",
                "DELETE FROM ProfessionalModelPartners WHERE ProfessionalModelId IN (SELECT Id FROM ProfessionalModels WHERE Title LIKE 'APDS1%')",
                $"DELETE FROM ProfessionalModelQuestions WHERE ModelId IN {Models}",
                "DELETE FROM ProfessionalModels WHERE Title LIKE 'APDS1%'",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'APDS1%'",
                $"DELETE FROM StudentBatchEnrollments WHERE BatchId IN {Batches}",
                "DELETE FROM Batches WHERE Name LIKE 'APDS1%'",
                "DELETE FROM Students WHERE NationalID LIKE 'APDS1%'",
                "DELETE FROM AspNetUsers WHERE UserName LIKE 'apds1-%'",
                $"DELETE FROM PartnerSubscriptionPeriods WHERE PartnerSubscriptionId IN (SELECT Id FROM PartnerSubscriptions WHERE PartnerId IN {Partners})",
                $"DELETE FROM PartnerSubscriptionCourses WHERE PartnerSubscriptionId IN (SELECT Id FROM PartnerSubscriptions WHERE PartnerId IN {Partners})",
                $"DELETE FROM PartnerSubscriptions WHERE PartnerId IN {Partners}",
                $"DELETE FROM CourseCurriculums WHERE CourseId IN {Courses}",
                "DELETE FROM Lessons WHERE Title LIKE 'APDS1%'",
                "DELETE FROM Units WHERE Title LIKE 'APDS1%'",
                "DELETE FROM Sections WHERE Title LIKE 'APDS1%'",
                "DELETE FROM Curriculums WHERE Title LIKE 'APDS1%'",
                "DELETE FROM Branches WHERE Name LIKE 'APDS1%'",
                "DELETE FROM Partners WHERE Code LIKE 'APDS1%'",
                "DELETE FROM Courses WHERE Name LIKE 'APDS1%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        // APD-S2: يجعل نموذج A قابلًا للإرسال لـ A وB، ويضيف نموذج اختبار لهما. يرجع رقم نموذج الاختبار.
        public static async Task<int> PrepareSendableModelsAsync(ApplicationDbContext db, ApdSeed seed)
        {
            var examModel = new ProfessionalModel
            {
                Title = $"{Marker} نموذج اختبار", Description = "-", CreatedBy = "-", CreatedAt = DateTime.UtcNow,
                IsForPartners = true, IsGlobal = false, ModelType = ProfessionalModelType.Exam, CurriculumId = seed.CurriculumId
            };
            db.ProfessionalModels.Add(examModel);
            await db.SaveChangesAsync();

            db.ProfessionalModelQuestions.AddRange(
                new ProfessionalModelQuestion { ModelId = seed.ModelLinkedToA, QuestionId = seed.QuestionId, OrderNumber = 1 },
                new ProfessionalModelQuestion { ModelId = seed.ModelNotForAnyone, QuestionId = seed.QuestionId, OrderNumber = 1 },
                new ProfessionalModelQuestion { ModelId = examModel.Id, QuestionId = seed.QuestionId, OrderNumber = 1 });

            db.ProfessionalModelPartners.AddRange(
                new ProfessionalModelPartner { ProfessionalModelId = seed.ModelLinkedToA, PartnerId = seed.PartnerB },
                new ProfessionalModelPartner { ProfessionalModelId = examModel.Id, PartnerId = seed.PartnerA },
                new ProfessionalModelPartner { ProfessionalModelId = examModel.Id, PartnerId = seed.PartnerB });

            var contractB = await db.PartnerSubscriptions.FirstAsync(s => s.PartnerId == seed.PartnerB);
            contractB.CanCreateHomework = true;
            db.PartnerSubscriptionPeriods.Add(new PartnerSubscriptionPeriod
            {
                PartnerSubscriptionId = contractB.Id, StartDate = DateTime.Today.AddDays(-1), EndDate = DateTime.Today.AddDays(30)
            });

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            return examModel.Id;
        }

        public static async Task<ApdPlacementSeed> PreparePlacementAsync(ApplicationDbContext db, ApdSeed seed)
        {
            var modelId = await PrepareSendableModelsAsync(db, seed);

            var contracts = await db.PartnerSubscriptions
                .Where(s => s.PartnerId == seed.PartnerA || s.PartnerId == seed.PartnerB)
                .ToListAsync();
            contracts.ForEach(c => c.CanUsePlacementExams = true);

            var branchA = await db.Batches.Where(b => b.Id == seed.BatchA1).Select(b => b.BranchId).FirstAsync();
            var batch = new Batch { Name = $"{Marker} دفعة A4", BranchId = branchA, CourseId = seed.CourseId, StartDate = DateTime.UtcNow };
            db.Batches.Add(batch);

            db.Users.Add(NewUser("apds1-sa4"));
            var student = new Student
            {
                NationalID = $"{Marker}A4", FullName = $"طالب {Marker} A4", Gender = "ذكر", Age = 17,
                School = "-", Level = "-", UserId = "apds1-sa4", BranchId = branchA, IsActiveForLearning = true
            };
            db.Students.Add(student);
            await db.SaveChangesAsync();

            db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = student.StudentID, BatchId = batch.Id });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            return new ApdPlacementSeed(modelId, batch.Id, student.StudentID);
        }

        public static async Task<ApdPerformanceSeed> PreparePerformanceAsync(ApplicationDbContext db, ApdSeed seed)
        {
            var contracts = await db.PartnerSubscriptions
                .Where(s => s.PartnerId == seed.PartnerA || s.PartnerId == seed.PartnerB)
                .ToListAsync();
            contracts.ForEach(c => c.CanUsePerformanceIndicatorExams = true);

            // سياق الشريك يتطلب فترة نشطة، والأساس يضيفها لعقد A فقط
            db.PartnerSubscriptionPeriods.Add(new PartnerSubscriptionPeriod
            {
                PartnerSubscriptionId = contracts.Single(c => c.PartnerId == seed.PartnerB).Id,
                StartDate = DateTime.Today.AddDays(-1),
                EndDate = DateTime.Today.AddDays(30)
            });

            var curriculum = new Curriculum { Title = $"{Marker} منهج مؤشر", Description = "-", CurriculumTypeName = "-" };
            var unit = new Unit { Title = $"{Marker} وحدة" };
            db.AddRange(curriculum, unit);
            await db.SaveChangesAsync();

            var section = new Section { Title = $"{Marker} محور", CurriculumId = curriculum.Id };
            db.Sections.Add(section);
            db.CourseCurriculums.Add(new CourseCurriculum { CourseId = seed.CourseId, CurriculumId = curriculum.Id });
            await db.SaveChangesAsync();

            var lesson = new Lesson { Title = $"{Marker} درس", Content = "-", SectionId = section.Id, UnitId = unit.Id };
            db.Lessons.Add(lesson);
            await db.SaveChangesAsync();

            Question NewQuestion(string suffix) => new()
            {
                Id = Guid.NewGuid(), Title = $"{Marker} سؤال {suffix}", ReferenceNumber = $"{Marker}-KPI-{suffix}",
                CurriculumId = curriculum.Id, SectionId = section.Id, LessonId = lesson.Id, CorrectAnswer = "أ",
                IsComplete = true, IsReviewed = true
            };

            var questionA = NewQuestion("A");
            var questionB = NewQuestion("B");
            db.Questions.AddRange(questionA, questionB);

            var template = new PerformanceIndicatorExam
            {
                Title = $"{Marker} قالب مؤشر", CurriculumId = curriculum.Id, ReferenceCode = "919191", TotalQuestions = 2, DurationMinutes = 2,
                PassPercent = 50, IsPartnerTemplate = true, IsArchived = true
            };
            db.PerformanceIndicatorExams.Add(template);
            await db.SaveChangesAsync();

            db.PerformanceIndicatorExamSections.Add(new PerformanceIndicatorExamSection
            {
                PerformanceIndicatorExamId = template.Id, SectionId = section.Id, QuestionCount = 2
            });
            db.PerformanceIndicatorExamQuestions.AddRange(
                new PerformanceIndicatorExamQuestion { PerformanceIndicatorExamId = template.Id, QuestionId = questionA.Id, SectionId = section.Id, OrderNumber = 1 },
                new PerformanceIndicatorExamQuestion { PerformanceIndicatorExamId = template.Id, QuestionId = questionB.Id, SectionId = section.Id, OrderNumber = 2 });
            await db.SaveChangesAsync();

            var studentB = await db.StudentBatchEnrollments.AsNoTracking()
                .Where(e => e.BatchId == seed.BatchB1)
                .Select(e => e.StudentID)
                .FirstAsync();

            db.ChangeTracker.Clear();
            return new ApdPerformanceSeed(curriculum.Id, template.Id, section.Id, questionA.Id, questionB.Id, studentB);
        }

        public static ApplicationUser NewUser(string userName) => new()
        {
            Id = userName,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = $"{userName}@test.local",
            NormalizedEmail = $"{userName}@test.local".ToUpperInvariant(),
            NationalID = $"{Marker}-{userName}",
            FullName = $"{Marker} {userName}",
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };

        public static async Task<ApdSeed> SeedAsync(ApplicationDbContext db)
        {
            var anchor = await db.Lessons.AsNoTracking()
                .Where(l => l.Section != null)
                .Select(l => new { LessonId = l.Id, l.Section.CurriculumId })
                .FirstAsync();
            var otherCurriculumId = await db.Curriculums.AsNoTracking().Where(c => c.Id != anchor.CurriculumId).Select(c => c.Id).FirstAsync();

            db.Users.Add(NewUser(SenderId));

            var today = DateTime.Today;
            Partner NewPartner(string code, bool active) => new()
            {
                Name = $"{Marker} مدرسة {code}",
                Code = $"{Marker}-{code}",
                PartnershipStart = today.AddDays(-30),
                PartnershipEnd = active ? today.AddDays(30) : today.AddDays(-1)
            };

            var a = NewPartner("A", true);
            var b = NewPartner("B", true);
            var c = NewPartner("C", true);
            var d = NewPartner("D", false);
            db.Partners.AddRange(a, b, c, d);

            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var otherCourse = new Course { Name = $"{Marker} دورة أخرى", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            db.Courses.AddRange(course, otherCourse);
            await db.SaveChangesAsync();

            db.CourseCurriculums.AddRange(
                new CourseCurriculum { CourseId = course.Id, CurriculumId = anchor.CurriculumId },
                new CourseCurriculum { CourseId = otherCourse.Id, CurriculumId = otherCurriculumId });

            var contractA = new PartnerSubscription
            {
                PartnerId = a.Id, StartDate = today.AddDays(-1), EndDate = today.AddDays(30),
                CanCreateHomework = true, CanCreateExams = true, CanUsePerformanceIndicatorExams = true,
                CanUseProfessionalModels = true, CanUsePlacementExams = false, CanUseMinistrySimExams = false
            };
            var contractB = new PartnerSubscription
            {
                PartnerId = b.Id, StartDate = today.AddDays(-1), EndDate = today.AddDays(30),
                CanCreateHomework = false, CanCreateExams = true, CanUseProfessionalModels = true, CanUseMinistrySimExams = true
            };
            // عقد منتهٍ لـ C: لا يُحسب عقدًا نشطًا
            var expiredC = new PartnerSubscription
            {
                PartnerId = c.Id, StartDate = today.AddDays(-60), EndDate = today.AddDays(-30),
                CanCreateHomework = true, CanCreateExams = true, CanUseProfessionalModels = true
            };
            db.PartnerSubscriptions.AddRange(contractA, contractB, expiredC);
            await db.SaveChangesAsync();

            db.PartnerSubscriptionPeriods.Add(new PartnerSubscriptionPeriod
            {
                PartnerSubscriptionId = contractA.Id, StartDate = today.AddDays(-1), EndDate = today.AddDays(30)
            });

            Branch NewBranch(string name, int? partnerId) => new()
            {
                Name = $"{Marker} {name}", PartnerId = partnerId, Location = "-", City = "-", State = "-", Country = "-"
            };

            var branchA = NewBranch("فرع A", a.Id);
            var branchB = NewBranch("فرع B", b.Id);
            var branchC = NewBranch("فرع C", c.Id);
            var institute = NewBranch("معهد", null);
            db.Branches.AddRange(branchA, branchB, branchC, institute);
            await db.SaveChangesAsync();

            Batch NewBatch(string name, int branchId, int courseId, bool archived = false)
            {
                var batch = new Batch { Name = $"{Marker} {name}", BranchId = branchId, CourseId = courseId, StartDate = DateTime.UtcNow, IsArchived = archived };
                db.Batches.Add(batch);
                return batch;
            }

            var a1 = NewBatch("دفعة A1", branchA.Id, course.Id);
            var aEmpty = NewBatch("دفعة A فارغة", branchA.Id, course.Id);
            var a2 = NewBatch("دفعة A2 مؤرشفة", branchA.Id, course.Id, archived: true);
            var a3 = NewBatch("دفعة A3 منهج آخر", branchA.Id, otherCourse.Id);
            var b1 = NewBatch("دفعة B1", branchB.Id, course.Id);
            var c1 = NewBatch("دفعة C1", branchC.Id, course.Id);
            var i1 = NewBatch("دفعة معهد 1", institute.Id, course.Id);
            var i2 = NewBatch("دفعة معهد 2", institute.Id, otherCourse.Id);
            await db.SaveChangesAsync();

            var n = 0;
            Student NewStudent(int branchId, bool active = true)
            {
                n++;
                var user = NewUser($"apds1-s{n}");
                db.Users.Add(user);
                var student = new Student
                {
                    NationalID = $"{Marker}{n}", FullName = $"طالب {Marker} {n}", Gender = "ذكر", Age = 17,
                    School = "-", Level = "-", UserId = user.Id, BranchId = branchId, IsActiveForLearning = active
                };
                db.Students.Add(student);
                return student;
            }

            var activeA = NewStudent(branchA.Id);
            var inactiveA = NewStudent(branchA.Id, active: false);
            var foreignB = NewStudent(branchB.Id);
            var leftA = NewStudent(branchA.Id);
            var inB = NewStudent(branchB.Id);
            var inI1 = NewStudent(institute.Id);
            var inC = NewStudent(branchC.Id);
            await db.SaveChangesAsync();

            db.StudentBatchEnrollments.AddRange(
                new StudentBatchEnrollment { StudentID = activeA.StudentID, BatchId = a1.Id },
                new StudentBatchEnrollment { StudentID = inactiveA.StudentID, BatchId = a1.Id },
                new StudentBatchEnrollment { StudentID = foreignB.StudentID, BatchId = a1.Id },
                new StudentBatchEnrollment { StudentID = leftA.StudentID, BatchId = a1.Id, Status = "Withdrawn" },
                new StudentBatchEnrollment { StudentID = inB.StudentID, BatchId = b1.Id },
                new StudentBatchEnrollment { StudentID = inC.StudentID, BatchId = c1.Id },
                new StudentBatchEnrollment { StudentID = inI1.StudentID, BatchId = i1.Id });

            ProfessionalModel NewModel(string title) => new()
            {
                Title = $"{Marker} {title}", Description = "-", CreatedBy = "-", CreatedAt = DateTime.UtcNow,
                IsForPartners = true, IsGlobal = false, ModelType = ProfessionalModelType.Homework, CurriculumId = anchor.CurriculumId
            };

            var linked = NewModel("نموذج A");
            var notForAnyone = NewModel("نموذج بلا شركاء");
            db.ProfessionalModels.AddRange(linked, notForAnyone);

            var question = new Question
            {
                Id = Guid.NewGuid(), Title = $"سؤال {Marker}", ReferenceNumber = $"{Marker}-Q1",
                CurriculumId = anchor.CurriculumId, LessonId = anchor.LessonId, CorrectAnswer = "أ",
                IsComplete = true, IsReviewed = true
            };
            db.Questions.Add(question);
            await db.SaveChangesAsync();

            db.ProfessionalModelPartners.Add(new ProfessionalModelPartner { ProfessionalModelId = linked.Id, PartnerId = a.Id });
            await db.SaveChangesAsync();

            return new ApdSeed(
                a.Id, b.Id, c.Id, d.Id,
                anchor.CurriculumId, otherCurriculumId, course.Id, otherCourse.Id,
                a1.Id, aEmpty.Id, a2.Id, a3.Id,
                b1.Id, c1.Id, i1.Id, i2.Id,
                activeA.StudentID, inI1.StudentID,
                linked.Id, notForAnyone.Id, anchor.LessonId, question.Id);
        }
    }
}
