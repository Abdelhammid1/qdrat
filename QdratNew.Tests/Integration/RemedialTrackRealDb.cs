using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // RTK-S7: زرع/تنظيف بيانات الخطة العلاجية على قاعدة الاختبار المعزولة (QdratNewDB_IntegrationTests) — مشترك بين
    // اختبارات SQL Server (S7.3) والتكامل HTTP (S7.2) وE2E (S7.2) والحمل (S7.3).
    // كل ما يُزرع يحمل علامة RTKS7 ويُحذف قبل التشغيل وبعده؛ لا اتصال بقاعدة الإنتاج مطلقًا.
    // مجموعة xUnit واحدة متسلسلة لأن كلها تكتب في القاعدة نفسها وتشغّل خادمًا على منفذ ثابت.
    [CollectionDefinition(Name)]
    public sealed class RemedialTrackSharedTestDbCollection
    {
        public const string Name = "RTK shared SQL test database";
    }

    public sealed record RtkSeedStudent(
        int StudentId, string UserId, string UserName, int EnrollmentId, int[] AxisProgressIds, int[] FirstAxisVideoProgressIds);

    public sealed class RtkSeed
    {
        public int BatchId { get; init; }
        public int TrackId { get; init; }
        public int[] AxisIds { get; init; } = Array.Empty<int>();
        public int[] VideoIds { get; init; } = Array.Empty<int>();    // فيديوهات المحور الأول بالترتيب
        public int Model101Id { get; init; }
        public int Model102Id { get; init; }
        public int PublicationId { get; init; }
        public string? AccessCode { get; init; }
        public List<RtkSeedStudent> Students { get; init; } = new();
    }

    internal static class RtkRealDb
    {
        public const string Marker = "RTKS7";
        public const string Password = "P@ssw0rd123!";
        public const string CorrectAnswer = "الجواب الصحيح RTKS7";
        public const string WrongAnswer = "جواب خاطئ RTKS7";
        public const string SecretExplanation = "شرح-سري-RTKS7-لا-يظهر-قبل-التسليم";
        public const string SecretVideoUrl = "https://example.com/rtks7-secret-video";

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerFactAttribute.EnvName));

        // نفس إعداد الإنتاج (EnableRetryOnFailure + UseCompatibilityLevel(120) كما في Program.cs)
        public static DbContextOptions<ApplicationDbContext> Options(string? connectionString = null)
            => new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connectionString ?? TestDatabaseBaseline.TestDatabaseConnectionString,
                    o => o.EnableRetryOnFailure().UseCompatibilityLevel(120).CommandTimeout(180))
                .Options;

        public static ApplicationDbContext CreateDb() => new(Options());

        public sealed class Factory : IDbContextFactory<ApplicationDbContext>
        {
            private readonly DbContextOptions<ApplicationDbContext> _options = Options();
            public ApplicationDbContext CreateDbContext() => new(_options);
        }

        public static string NewAccessCode() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        public static async Task<RtkSeed> SeedAsync(
            int students,
            bool inPerson = false,
            bool withLogin = false,
            int axes = 2,
            int videosPerAxis = 2,
            int questionsPerModel = 4,
            RemedialTrackAxisStatus firstAxisStatus = RemedialTrackAxisStatus.Videos)
        {
            await TestDatabaseBaseline.EnsureAsync();
            await CleanupAsync();

            await using var db = CreateDb();
            db.ChangeTracker.AutoDetectChangesEnabled = false;

            var anchor = await db.Questions.AsNoTracking()
                .Where(q => q.SectionId != null)
                .Select(q => new { q.CurriculumId, q.LessonId })
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("قاعدة الاختبار بلا أي سؤال لاستعارة المنهج/الدرس.");

            var branch = new Branch { Name = $"{Marker} فرع", Location = "-", City = "-", State = "-", Country = "-" };
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            db.Branches.Add(branch);
            db.Courses.Add(course);
            await db.SaveChangesAsync();

            var batch = new Batch { Name = $"{Marker} دفعة", StartDate = DateTime.UtcNow, CourseId = course.Id, BranchId = branch.Id };
            db.Batches.Add(batch);

            var m101 = new ProfessionalModel { Title = $"{Marker}-M101", Description = "d", CreatedBy = "rtks7" };
            var m102 = new ProfessionalModel { Title = $"{Marker}-M102", Description = "d", CreatedBy = "rtks7" };
            db.ProfessionalModels.AddRange(m101, m102);
            await db.SaveChangesAsync();

            foreach (var model in new[] { m101, m102 })
                for (var i = 1; i <= questionsPerModel; i++)
                {
                    var q = new Question
                    {
                        Id = Guid.NewGuid(),
                        Title = $"سؤال {Marker} {model.Id}-{i}",
                        ReferenceNumber = $"{Marker}-{model.Id}-{i}",
                        CurriculumId = anchor.CurriculumId,
                        LessonId = anchor.LessonId,
                        CorrectAnswer = CorrectAnswer,
                        Explanation = SecretExplanation,
                        VideoUrl = SecretVideoUrl,
                        IsComplete = true,
                        Options = new List<QuestionOption>
                        {
                            new() { Text = CorrectAnswer }, new() { Text = WrongAnswer }, new() { Text = "ج RTKS7" }, new() { Text = "د RTKS7" }
                        }
                    };
                    db.Questions.Add(q);
                    db.ProfessionalModelQuestions.Add(new ProfessionalModelQuestion { ModelId = model.Id, QuestionId = q.Id, OrderNumber = i });
                }
            await db.SaveChangesAsync();

            var track = new RemedialTrack
            {
                Code = $"{Marker}-{Guid.NewGuid():N}"[..14],
                Title = $"{Marker} خطة علاجية",
                CurriculumId = anchor.CurriculumId,
                Status = RemedialTrackStatus.Ready,
                MinWatchPercent = 90,
                PassPercent = 60,
                IsStructureLocked = true,
                CreatedByUserId = "rtks7-admin",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
            };
            db.RemedialTracks.Add(track);
            await db.SaveChangesAsync();

            var axisEntities = new List<RemedialTrackAxis>();
            for (var i = 1; i <= axes; i++)
            {
                var sec = new Section { Title = $"{Marker} قسم {i}", CurriculumId = anchor.CurriculumId };
                db.Sections.Add(sec);
                await db.SaveChangesAsync();
                axisEntities.Add(new RemedialTrackAxis
                {
                    TrackId = track.Id, SectionId = sec.Id, Order = i, TitleOverride = $"{Marker} محور {i}",
                    Exam101ModelId = m101.Id, Exam102ModelId = m102.Id, ExamDurationMinutes = 30
                });
            }
            db.RemedialTrackAxes.AddRange(axisEntities);
            await db.SaveChangesAsync();

            var videos = new List<RemedialTrackVideo>();
            foreach (var a in axisEntities)
                for (var v = 1; v <= videosPerAxis; v++)
                    videos.Add(new RemedialTrackVideo
                    {
                        AxisId = a.Id, Order = v, Title = $"{Marker} فيديو {a.Order}-{v}",
                        Url = $"https://youtu.be/rtks7{a.Order}{v}aaaa"[..28],
                        Provider = RemedialTrackVideoProvider.YouTube, ExternalId = $"rtks7{a.Order}{v}aaaa"[..11],
                        DurationSeconds = 100
                    });
            db.RemedialTrackVideos.AddRange(videos);
            await db.SaveChangesAsync();

            var code = inPerson ? NewAccessCode() : null;
            var pub = new RemedialTrackPublication
            {
                TrackId = track.Id, BatchId = batch.Id, Scope = RemedialTrackPublicationScope.WholeBatch,
                Mode = inPerson ? RemedialTrackDeliveryMode.InPerson : RemedialTrackDeliveryMode.Online,
                PublishAtUtc = DateTime.UtcNow.AddHours(-1), Status = RemedialTrackPublicationStatus.Active,
                AccessCode = code, CodeVersion = 1, CodeGeneratedAtUtc = inPerson ? DateTime.UtcNow : null,
                CreatedByUserId = "rtks7-admin", CreatedAtUtc = DateTime.UtcNow.AddDays(-2), TotalStudents = students
            };
            db.RemedialTrackPublications.Add(pub);
            await db.SaveChangesAsync();

            // مستخدمو Identity: Hash واحد مُعاد استخدامه (التجزئة بطيئة عمدًا)
            var hasher = new PasswordHasher<ApplicationUser>();
            string? hash = null;
            string? studentRoleId = null;
            if (withLogin)
            {
                studentRoleId = await db.Roles.Where(r => r.Name == "Student").Select(r => r.Id).FirstOrDefaultAsync()
                    ?? throw new InvalidOperationException("دور 'Student' غير موجود في قاعدة الاختبار.");
            }

            var seeded = new List<RtkSeedStudent>();
            const int chunk = 100;
            for (var start = 1; start <= students; start += chunk)
            {
                var end = Math.Min(students, start + chunk - 1);
                var users = new List<ApplicationUser>();
                var stus = new List<Student>();
                for (var i = start; i <= end; i++)
                {
                    var userName = $"rtks7-s{i:D4}";
                    var user = new ApplicationUser
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserName = userName, NormalizedUserName = userName.ToUpperInvariant(),
                        Email = $"{userName}@test.local", NormalizedEmail = $"{userName}@test.local".ToUpperInvariant(),
                        EmailConfirmed = true, NationalID = $"RS7{i:D7}", FullName = $"طالب {Marker} {i}",
                        SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                        IsActive = true
                    };
                    if (withLogin) user.PasswordHash = hash ??= hasher.HashPassword(user, Password);
                    users.Add(user);
                    stus.Add(new Student
                    {
                        NationalID = $"RS7{i:D7}", FullName = $"طالب {Marker} {i}", Gender = "ذكر", Age = 17,
                        School = "-", Level = "-", UserId = user.Id, BranchId = branch.Id
                    });
                }
                db.Users.AddRange(users);
                db.Students.AddRange(stus);
                await db.SaveChangesAsync();

                if (withLogin)
                {
                    db.UserRoles.AddRange(users.Select(u => new IdentityUserRole<string> { UserId = u.Id, RoleId = studentRoleId! }));
                    await db.SaveChangesAsync();
                }

                var enrollments = new List<RemedialTrackEnrollment>();
                for (var k = 0; k < stus.Count; k++)
                {
                    var e = new RemedialTrackEnrollment
                    {
                        PublicationId = pub.Id, TrackId = track.Id, StudentId = stus[k].StudentID,
                        CreatedAtUtc = DateTime.UtcNow.AddDays(-2), Status = RemedialTrackEnrollmentStatus.InProgress,
                        CurrentAxisId = axisEntities[0].Id, StartedAtUtc = DateTime.UtcNow.AddHours(-1)
                    };
                    foreach (var a in axisEntities)
                        e.AxisProgresses.Add(new RemedialTrackAxisProgress
                        {
                            AxisId = a.Id, Order = a.Order,
                            Status = a.Order == 1 ? firstAxisStatus : RemedialTrackAxisStatus.Locked, Round = 1,
                            OpenedAtUtc = a.Order == 1 ? DateTime.UtcNow.AddHours(-1) : null
                        });
                    enrollments.Add(e);
                }
                db.RemedialTrackEnrollments.AddRange(enrollments);
                await db.SaveChangesAsync();

                // صفوف تقدّم فيديوهات المحور الأول (الجولة 1) كما تنشئها الخدمة عند فتح المحور
                var firstAxis = axisEntities[0];
                var firstVideos = videos.Where(v => v.AxisId == firstAxis.Id).OrderBy(v => v.Order).ToList();
                var vps = new List<RemedialTrackVideoProgress>();
                foreach (var e in enrollments)
                {
                    var ap = e.AxisProgresses.First(a => a.AxisId == firstAxis.Id);
                    foreach (var v in firstVideos)
                        vps.Add(new RemedialTrackVideoProgress
                        {
                            AxisProgressId = ap.Id, VideoId = v.Id, VideoOrder = v.Order, Round = 1, DurationSeconds = v.DurationSeconds
                        });
                }
                db.RemedialTrackVideoProgresses.AddRange(vps);
                await db.SaveChangesAsync();

                for (var k = 0; k < stus.Count; k++)
                {
                    var e = enrollments[k];
                    var apIds = e.AxisProgresses.OrderBy(a => a.Order).Select(a => a.Id).ToArray();
                    var vpIds = vps.Where(x => x.AxisProgressId == apIds[0]).OrderBy(x => x.VideoOrder).Select(x => x.Id).ToArray();
                    seeded.Add(new RtkSeedStudent(stus[k].StudentID, users[k].Id, users[k].UserName!, e.Id, apIds, vpIds));
                }
            }

            return new RtkSeed
            {
                BatchId = batch.Id, TrackId = track.Id,
                AxisIds = axisEntities.Select(a => a.Id).ToArray(),
                VideoIds = videos.Where(v => v.AxisId == axisEntities[0].Id).OrderBy(v => v.Order).Select(v => v.Id).ToArray(),
                Model101Id = m101.Id, Model102Id = m102.Id,
                PublicationId = pub.Id, AccessCode = code, Students = seeded
            };
        }

        public static async Task CleanupAsync()
        {
            await using var db = CreateDb();

            const string tracks = "(SELECT Id FROM RemedialTracks WHERE Code LIKE 'RTKS7-%')";
            const string axes = $"(SELECT Id FROM RemedialTrackAxes WHERE TrackId IN {tracks})";
            const string enr = $"(SELECT Id FROM RemedialTrackEnrollments WHERE TrackId IN {tracks})";
            const string aps = $"(SELECT Id FROM RemedialTrackAxisProgresses WHERE EnrollmentId IN {enr})";
            const string atts = $"(SELECT Id FROM RemedialTrackExamAttempts WHERE AxisProgressId IN {aps})";
            const string users = "(SELECT Id FROM AspNetUsers WHERE UserName LIKE 'rtks7-%')";
            const string stu = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'RS7%')";
            const string models = "(SELECT Id FROM ProfessionalModels WHERE Title LIKE 'RTKS7-%')";
            const string questions = "(SELECT Id FROM Questions WHERE ReferenceNumber LIKE 'RTKS7-%')";

            foreach (var sql in new[]
            {
                $"DELETE FROM RemedialTrackExamAttemptQuestions WHERE AttemptId IN {atts}",
                $"DELETE FROM RemedialTrackExamAttempts WHERE AxisProgressId IN {aps}",
                $"DELETE FROM RemedialTrackVideoProgresses WHERE AxisProgressId IN {aps}",
                $"DELETE FROM RemedialTrackEvents WHERE EnrollmentId IN {enr}",
                $"DELETE FROM RemedialTrackAxisProgresses WHERE EnrollmentId IN {enr}",
                $"DELETE FROM RemedialTrackEnrollments WHERE TrackId IN {tracks}",
                $"DELETE FROM RemedialTrackPublications WHERE TrackId IN {tracks}",
                $"DELETE FROM RemedialTrackVideos WHERE AxisId IN {axes}",
                $"DELETE FROM RemedialTrackAxes WHERE TrackId IN {tracks}",
                "DELETE FROM RemedialTracks WHERE Code LIKE 'RTKS7-%'",
                $"DELETE FROM ProfessionalModelQuestions WHERE ModelId IN {models}",
                $"DELETE FROM QuestionOptions WHERE QuestionId IN {questions}",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'RTKS7-%'",
                "DELETE FROM ProfessionalModels WHERE Title LIKE 'RTKS7-%'",
                "DELETE FROM Sections WHERE Title LIKE 'RTKS7 %'",
                $"DELETE FROM StudentBatchEnrollments WHERE StudentID IN {stu}",
                $"DELETE FROM Notifications WHERE UserId IN {users}",
                "DELETE FROM Students WHERE NationalID LIKE 'RS7%'",
                $"DELETE FROM AspNetUserRoles WHERE UserId IN {users}",
                "DELETE FROM AspNetUsers WHERE UserName LIKE 'rtks7-%'",
                "DELETE FROM Batches WHERE Name LIKE 'RTKS7 %'",
                "DELETE FROM Courses WHERE Name LIKE 'RTKS7 %'",
                "DELETE FROM Branches WHERE Name LIKE 'RTKS7 %'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }
    }
}
