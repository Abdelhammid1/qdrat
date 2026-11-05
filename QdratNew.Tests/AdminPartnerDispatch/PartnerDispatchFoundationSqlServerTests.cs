using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Migrations;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services.PartnerDispatch;
using QdratNew.Services.PartnerHomework;
using QdratNew.Tests.Integration;
using QdratNew.ViewModels.Partner.HomeworkDraft;
using Xunit;

namespace QdratNew.Tests.AdminPartnerDispatch
{
    // APD-S1.2/S1.3 — سجل الإرسال داخل Transaction المستدعي، المنح المبدئي لـ PartnerDispatch.Send، وملكية ما يرسله الشريك.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "SqlServer")]
    public sealed class PartnerDispatchFoundationSqlServerTests : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;
            await using var db = RtkRealDb.CreateDb();
            await ApdTestData.CleanupAsync(db);
        }

        public async Task DisposeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;
            await using var db = RtkRealDb.CreateDb();
            await ApdTestData.CleanupAsync(db);
        }

        private static async Task<ApdSeed> SeedAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            return await ApdTestData.SeedAsync(db);
        }

        private static List<PartnerDispatchLog> Entries(ApdSeed seed, int count, Guid group) => Enumerable.Range(0, count)
            .Select(i => new PartnerDispatchLog
            {
                DispatchGroupId = group,
                Kind = PartnerDispatchKind.Homework,
                Title = $"{ApdTestData.Marker} إرسال {i}",
                SourceProfessionalModelId = seed.ModelLinkedToA,
                PartnerId = i % 2 == 0 ? seed.PartnerA : seed.PartnerB,
                BatchId = i % 2 == 0 ? seed.BatchA1 : seed.BatchB1,
                TargetEntityId = 1000 + i,
                StudentsCount = 3,
                SentByUserId = ApdTestData.SenderId
            })
            .ToList();

        // =====================================================
        // سجل الإرسال
        // =====================================================
        [SqlServerFact]
        public async Task Log_IsWrittenInsideCallerTransaction_AndRolledBackWithIt()
        {
            var seed = await SeedAsync();
            var group = Guid.NewGuid();

            await using (var db = RtkRealDb.CreateDb())
            {
                var service = new PartnerDispatchLogService(db);
                await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync();
                    await service.WriteAsync(Entries(seed, 2, group));
                    await service.WriteAsync(Entries(seed, 25, group)); // مسار BulkInsert داخل نفس الـ Transaction
                    await tx.RollbackAsync();
                });
            }

            await using var read = RtkRealDb.CreateDb();
            Assert.Equal(0, await read.PartnerDispatchLogs.CountAsync(l => l.DispatchGroupId == group));
        }

        [SqlServerFact]
        public async Task Log_SmallAndBulkWrites_AreCommitted_AndNormalized()
        {
            var seed = await SeedAsync();
            var group = Guid.NewGuid();

            var small = Entries(seed, 2, group);
            small[0].Title = $"{ApdTestData.Marker} " + new string('ع', 400);
            small[0].OverrideReason = "سبب بلا تجاوز يجب ألا يُحفظ";
            small[1].ContractOverridden = true;
            small[1].OverrideReason = "  " + new string('س', 600);

            await using (var db = RtkRealDb.CreateDb())
            {
                var service = new PartnerDispatchLogService(db);
                await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync();
                    await service.WriteAsync(small);
                    await service.WriteAsync(Entries(seed, 25, group));
                    await tx.CommitAsync();
                });
            }

            await using var read = RtkRealDb.CreateDb();
            var rows = await new PartnerDispatchLogService(read).Query().Where(l => l.DispatchGroupId == group).ToListAsync();

            Assert.Equal(27, rows.Count);
            Assert.All(rows, r => Assert.True(r.SentAtUtc > DateTime.UtcNow.AddMinutes(-5) && r.SentAtUtc <= DateTime.UtcNow.AddMinutes(1)));

            var truncated = rows.Single(r => r.Title.Length == 300);
            Assert.Null(truncated.OverrideReason);

            var overridden = rows.Single(r => r.ContractOverridden);
            Assert.Equal(500, overridden.OverrideReason!.Length);
            Assert.StartsWith("س", overridden.OverrideReason);
        }

        [SqlServerFact]
        public async Task Log_RejectsEntryWithoutPartnerGroupOrSender()
        {
            var seed = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();
            var service = new PartnerDispatchLogService(db);

            var noPartner = Entries(seed, 1, Guid.NewGuid());
            noPartner[0].PartnerId = 0;
            await Assert.ThrowsAsync<ArgumentException>(() => service.WriteAsync(noPartner));

            var noGroup = Entries(seed, 1, Guid.Empty);
            await Assert.ThrowsAsync<ArgumentException>(() => service.WriteAsync(noGroup));

            var noSender = Entries(seed, 1, Guid.NewGuid());
            noSender[0].SentByUserId = " ";
            await Assert.ThrowsAsync<ArgumentException>(() => service.WriteAsync(noSender));
        }

        // =====================================================
        // المنح المبدئي (نفس SQL الـ Migration)
        // =====================================================
        [SqlServerFact]
        public async Task GrantSql_GivesSendOnly_ToProfilesThatManageModelHomework()
        {
            int implicitRw, explicitCustom, readOnly, customWithoutIt, alreadyConfigured;

            await using (var db = RtkRealDb.CreateDb())
            {
                async Task<int> Profile(string name, AdminControllerAccessLevel level, bool hasCustom, params string[] allowed)
                {
                    var profile = new AdminProfile { Name = $"{ApdTestData.Marker} {name}" };
                    db.AdminProfiles.Add(profile);
                    await db.SaveChangesAsync();

                    var perm = new AdminProfileControllerPermission
                    {
                        AdminProfileId = profile.Id, ControllerName = "AdminLessonCompletions", AccessLevel = level, HasCustomPermissions = hasCustom
                    };
                    foreach (var action in allowed)
                        perm.CustomPermissions.Add(new AdminProfileCustomPermission { ActionName = action, IsAllowed = true });
                    db.AdminProfileControllerPermissions.Add(perm);
                    await db.SaveChangesAsync();
                    return profile.Id;
                }

                implicitRw = await Profile("قراءة وكتابة", AdminControllerAccessLevel.ReadWrite, false);
                explicitCustom = await Profile("مخصص", AdminControllerAccessLevel.Read, true, "ManageModelHomework");
                readOnly = await Profile("قراءة", AdminControllerAccessLevel.Read, false);
                customWithoutIt = await Profile("مخصص بلا واجبات النماذج", AdminControllerAccessLevel.ReadWrite, true, "ManageModelExam");
                alreadyConfigured = await Profile("مُعد مسبقًا", AdminControllerAccessLevel.ReadWrite, false);

                var existing = new AdminProfileControllerPermission
                {
                    AdminProfileId = alreadyConfigured, ControllerName = "PartnerDispatch", AccessLevel = AdminControllerAccessLevel.Read, HasCustomPermissions = true
                };
                existing.CustomPermissions.Add(new AdminProfileCustomPermission { ActionName = "ReadLog", IsAllowed = true });
                db.AdminProfileControllerPermissions.Add(existing);
                await db.SaveChangesAsync();

                await db.Database.ExecuteSqlRawAsync(APD_PartnerDispatch.GrantSendSql);
                await db.Database.ExecuteSqlRawAsync(APD_PartnerDispatch.GrantSendSql); // إعادة التشغيل لا تكرر
            }

            await using var read = RtkRealDb.CreateDb();
            async Task<List<string>> DispatchActions(int profileId) => await read.AdminProfileCustomPermissions
                .Where(c => c.ControllerPermission.AdminProfileId == profileId && c.ControllerPermission.ControllerName == "PartnerDispatch" && c.IsAllowed)
                .Select(c => c.ActionName)
                .ToListAsync();

            Assert.Equal(new[] { "Send" }, await DispatchActions(implicitRw));
            Assert.Equal(new[] { "Send" }, await DispatchActions(explicitCustom));
            Assert.Empty(await DispatchActions(readOnly));
            Assert.Empty(await DispatchActions(customWithoutIt));
            Assert.Equal(new[] { "ReadLog" }, await DispatchActions(alreadyConfigured)); // لا يُمس ما ضبطه الأدمن

            Assert.Equal(1, await read.AdminProfileControllerPermissions.CountAsync(p => p.AdminProfileId == implicitRw && p.ControllerName == "PartnerDispatch"));

            // الصلاحية الممنوحة تعمل في الـ Handler: Send نعم، Override لا
            var userId = "apds1-profile-user";
            read.Users.Add(ApdTestData.NewUser(userId));
            read.AdminUserProfiles.Add(new AdminUserProfile { UserId = userId, AdminProfileId = explicitCustom });
            await read.SaveChangesAsync();

            async Task<bool> Can(string policy)
            {
                var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Role, "Admin")
                }, "test"));
                var ctx = new AuthorizationHandlerContext(new[] { new AdminPermissionAuthorizationRequirement(policy) }, user, null);
                await new AdminPermissionAuthorizationHandler(read).HandleAsync(ctx);
                return ctx.HasSucceeded;
            }

            Assert.True(await Can(AdminPermissionPolicies.PartnerDispatch_Send));
            Assert.False(await Can(AdminPermissionPolicies.PartnerDispatch_Override));
            Assert.False(await Can(AdminPermissionPolicies.PartnerDispatch_ReadLog));
        }

        // =====================================================
        // ملكية ما يرسله الشريك من مسوداته
        // =====================================================
        [SqlServerFact]
        public async Task PartnerDraftSend_SetsOwnerPartner()
        {
            var seed = await SeedAsync();
            int draftId, periodId;

            await using (var db = RtkRealDb.CreateDb())
            {
                periodId = await db.PartnerSubscriptionPeriods
                    .Where(p => p.PartnerSubscription.PartnerId == seed.PartnerA)
                    .Select(p => p.Id)
                    .SingleAsync();
                var sectionId = await db.Lessons.Where(l => l.Id == seed.LessonId).Select(l => l.SectionId).SingleAsync();

                var draft = new HomeworkDraft
                {
                    PartnerId = seed.PartnerA, SubscriptionPeriodId = periodId, CourseId = seed.CourseId,
                    Title = $"{ApdTestData.Marker} مسودة", QuestionsPerLesson = 1,
                    Questions = new List<HomeworkDraftQuestion>
                    {
                        new() { QuestionId = seed.QuestionId, LessonId = seed.LessonId, SectionId = sectionId, Order = 1 }
                    }
                };
                db.HomeworkDrafts.Add(draft);
                await db.SaveChangesAsync();
                draftId = draft.Id;
            }

            await using (var db = RtkRealDb.CreateDb())
            {
                new PartnerHomeworkAssignmentService(db).SendDraftToBatches(
                    new SendHomeworkDraftVM { DraftId = draftId, BatchIds = new List<int> { seed.BatchA1 }, EndAt = DateTime.UtcNow.AddDays(3) },
                    seed.PartnerA,
                    periodId);
            }

            await using var read = RtkRealDb.CreateDb();
            var set = await read.HomeworkSets.AsNoTracking().SingleAsync(h => h.BatchId == seed.BatchA1);
            Assert.Equal(seed.PartnerA, set.OwnerPartnerId);
        }
    }
}
