using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Tests.AdminPartnerDispatch;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.OnlineLecture
{
    /// <summary>
    /// OLC-S5 عبر HTTP على قاعدة الاختبار (SQL Server): قسم «المحتوى التعليمي» للطالب — الوصول (R3/R17)، القفل التسلسلي
    /// (R13/R15)، عدم إرجاع أي معرّف فيديو (C.5)، رابط القائمة للمؤهلين فقط، و404 مع المفتاح المغلق.
    /// يعيد استخدام بيانات APD: A1 دفعة شريك A (البند مفعّل هنا) فيها طالب نشط + منسحب، B1 دفعة شريك B بلا البند.
    /// </summary>
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class StudentOnlineLearningIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string FeatureKey = "OnlineLectureContent.Enabled";
        private const string FeatureCacheKey = "olc-feature-enabled";
        private const string FullVideoId = "dQw4w9WgXcQ";
        private const string ItemVideoId = "987654321";
        private const string LockMessage = "أكمل المحاضرة السابقة أولًا";

        private readonly CustomWebApplicationFactory _factory;
        private ApdSeed _seed = null!;
        private int _content1, _content2;
        private string _studentAUser = null!, _studentBUser = null!, _withdrawnUser = null!;
        private string? _featureBefore;

        public StudentOnlineLearningIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await CleanupOlcAsync(db);
            await ApdTestData.CleanupAsync(db);
            _seed = await ApdTestData.SeedAsync(db);

            // A: البند مفعّل (R17)؛ B: عقد نشط بلا البند. A1 وB1 أونلاين.
            var contractA = await db.PartnerSubscriptions.SingleAsync(s => s.PartnerId == _seed.PartnerA);
            contractA.CanAccessEducationalContent = true;
            await db.SaveChangesAsync();
            await db.Batches.Where(b => b.Id == _seed.BatchA1 || b.Id == _seed.BatchB1)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsOnlineLearning, true));

            var anchor = await db.Lecture.AsNoTracking().Select(l => new { l.InstructorId }).FirstAsync();
            var sectionId = await db.Lessons.AsNoTracking().Where(l => l.Id == _seed.LessonId).Select(l => l.SectionId).FirstAsync();
            Lecture NewLecture(int batchId, int daysAgo) => new()
            {
                Title = $"{ApdTestData.Marker} محاضرة الطالب {daysAgo}", Location = "-", Date = DateTime.Today.AddDays(-daysAgo),
                InstructorId = anchor.InstructorId, SectionId = sectionId, CourseId = _seed.CourseId, BatchId = batchId
            };
            var l1 = NewLecture(_seed.BatchA1, 2);
            var l2 = NewLecture(_seed.BatchA1, 1);
            var lb = NewLecture(_seed.BatchB1, 1);
            db.Lecture.AddRange(l1, l2, lb);
            await db.SaveChangesAsync();

            var c1 = new LectureContent
            {
                LectureId = l1.Id, Title = "OLCS5 الفيديو الكامل", ContentMode = LectureContentMode.FullVideo, Status = LectureContentStatus.Published,
                VideoProvider = LectureVideoProvider.YouTube, VideoUrl = $"https://www.youtube.com/watch?v={FullVideoId}", VideoExternalId = FullVideoId,
                DurationSeconds = 600, CreatedAt = DateTime.UtcNow
            };
            var c2 = new LectureContent
            {
                LectureId = l2.Id, Title = "OLCS5 قائمة المؤشرات", ContentMode = LectureContentMode.IndicatorVideos, Status = LectureContentStatus.Published,
                CreatedAt = DateTime.UtcNow
            };
            c2.Items.Add(new LectureContentItem
            {
                LessonId = _seed.LessonId, SortOrder = 0, VideoProvider = LectureVideoProvider.Vimeo,
                VideoUrl = $"https://vimeo.com/{ItemVideoId}", VideoExternalId = ItemVideoId, CreatedAt = DateTime.UtcNow
            });
            var cb = new LectureContent
            {
                LectureId = lb.Id, Title = "OLCS5 محتوى دفعة B", ContentMode = LectureContentMode.FullVideo, Status = LectureContentStatus.Published,
                DurationSeconds = 600, CreatedAt = DateTime.UtcNow
            };
            db.LectureContents.AddRange(c1, c2, cb);

            var setting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == FeatureKey);
            _featureBefore = setting?.Value;
            if (setting == null)
                db.SystemSettings.Add(new SystemSetting { Key = FeatureKey, Value = "true" });
            else
                setting.Value = "true";
            await db.SaveChangesAsync();
            _content1 = c1.Id;
            _content2 = c2.Id;

            var users = await db.Students.AsNoTracking()
                .Where(s => s.NationalID.StartsWith(ApdTestData.Marker))
                .Select(s => new { s.StudentID, s.UserId, s.BranchId })
                .ToListAsync();
            _studentAUser = users.Single(u => u.StudentID == _seed.ActiveStudentA).UserId!;

            // طالب B: مسجّل نشط في B1 (شريك بلا البند). المنسحب: Withdrawn في A1.
            var inB = await db.StudentBatchEnrollments.AsNoTracking()
                .Where(e => e.BatchId == _seed.BatchB1 && e.Status == "Active").Select(e => e.StudentID).FirstAsync();
            _studentBUser = users.Single(u => u.StudentID == inB).UserId!;
            var withdrawn = await db.StudentBatchEnrollments.AsNoTracking()
                .Where(e => e.BatchId == _seed.BatchA1 && e.Status == "Withdrawn").Select(e => e.StudentID).FirstAsync();
            _withdrawnUser = users.Single(u => u.StudentID == withdrawn).UserId!;

            ResetCaches();
        }

        public async Task DisposeAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await CleanupOlcAsync(db);
            await ApdTestData.CleanupAsync(db);

            if (_featureBefore == null)
                await db.SystemSettings.Where(s => s.Key == FeatureKey).ExecuteDeleteAsync();
            else
                await db.SystemSettings.Where(s => s.Key == FeatureKey).ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, _featureBefore));

            ResetCaches();
        }

        private static async Task CleanupOlcAsync(ApplicationDbContext db)
        {
            const string lectures = "(SELECT Id FROM [Lecture] WHERE Title LIKE 'APDS1%')";
            const string contents = "(SELECT Id FROM LectureContents WHERE LectureId IN " + lectures + ")";
            foreach (var sql in new[]
            {
                $"DELETE FROM LectureContentItemProgress WHERE LectureContentItemId IN (SELECT Id FROM LectureContentItems WHERE LectureContentId IN {contents})",
                $"DELETE FROM LectureContentProgress WHERE LectureContentId IN {contents}",
                $"DELETE FROM LectureContentItems WHERE LectureContentId IN {contents}",
                $"DELETE FROM LectureContents WHERE LectureId IN {lectures}",
                "DELETE FROM [Lecture] WHERE Title LIKE 'APDS1%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        // كاش المفتاح (30ث) وكاش رابط القائمة (5د) في ذاكرة التطبيق — يُمسحان بين الخطوات حتى تُختبر القيمة الحالية
        private void ResetCaches()
        {
            var cache = _factory.Services.GetRequiredService<IMemoryCache>();
            cache.Remove(FeatureCacheKey);
            if (cache is MemoryCache concrete)
                concrete.Compact(1.0);
        }

        private HttpClient StudentClient(string userId)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Student");
            return client;
        }

        private static async Task<(HttpStatusCode Status, string Html)> GetAsync(HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            return (response.StatusCode, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        }

        private async Task DbAsync(Func<ApplicationDbContext, Task> action)
        {
            using var scope = _factory.Services.CreateScope();
            await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        [Fact]
        public async Task Student_SeesCurriculaWithServerLock_NoVideoIds_AndUnlocksAfterCompletion()
        {
            var client = StudentClient(_studentAUser);

            var (status, html) = await GetAsync(client, "/Students/OnlineLearning");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Contains("OLCS5 الفيديو الكامل", html);
            Assert.Contains("OLCS5 قائمة المؤشرات", html);
            Assert.DoesNotContain("OLCS5 محتوى دفعة B", html);       // دفعة أخرى لا تظهر
            Assert.Contains(LockMessage, html);                     // الثانية مقفلة حتى إتمام الأولى (R13/R15)
            Assert.Contains("تابع من هنا", html);
            Assert.DoesNotContain(FullVideoId, html);                // C.5: لا معرّف ولا رابط فيديو
            Assert.DoesNotContain(ItemVideoId, html);
            Assert.DoesNotContain("youtube", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("vimeo", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("href=\"/Students/OnlineLearning\"", html);   // رابط القائمة (R3)

            // إتمام الأولى من الخادم ⇒ الثانية تُفتح
            await DbAsync(async db =>
            {
                db.LectureContentProgress.Add(new LectureContentProgress
                {
                    LectureContentId = _content1, StudentId = _seed.ActiveStudentA, WatchedSeconds = 600, MaxPositionSeconds = 600,
                    DurationSeconds = 600, EndedSeen = true, IsCompleted = true, CompletedAtUtc = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            });

            (status, html) = await GetAsync(client, "/Students/OnlineLearning");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.DoesNotContain(LockMessage, html);
            Assert.Contains("مكتمل 1 من 2", html);

            // عنصر محذوف ناعمًا لا يُحسب ولا يكسر الاستعلام (QueryFilter داخل التجميع على SQL Server)
            await DbAsync(db => db.LectureContentItems.Where(i => i.LectureContentId == _content2)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsDeleted, true)));
            (status, _) = await GetAsync(client, "/Students/OnlineLearning");
            Assert.Equal(HttpStatusCode.OK, status);
        }

        [Fact]
        public async Task PartnerBatchWithoutClause_And_WithdrawnStudent_SeeUnavailable_WithoutMenuLink()
        {
            foreach (var user in new[] { _studentBUser, _withdrawnUser })
            {
                var (status, html) = await GetAsync(StudentClient(user), "/Students/OnlineLearning");
                Assert.Equal(HttpStatusCode.OK, status);
                Assert.Contains("المحتوى التعليمي غير متاح لدورتك الحالية", html);
                Assert.DoesNotContain("OLCS5", html);
                Assert.DoesNotContain("href=\"/Students/OnlineLearning\"", html);
            }
        }

        [Fact]
        public async Task MenuLink_UpdatesImmediately_WhenAdminTogglesBatch_WithoutWaitingForCache()
        {
            var client = StudentClient(_studentAUser);
            const string link = "href=\"/Students/OnlineLearning\"";

            var (_, before) = await GetAsync(client, "/Students/OnlineLearning");
            Assert.Contains(link, before);   // النتيجة الآن في الكاش (true)

            // إلغاء الدفعة من خدمة الأدمن (نفس مسار Batches/SetOnlineLearning) — بلا مسح يدوي للكاش
            using (var scope = _factory.Services.CreateScope())
            {
                var admin = scope.ServiceProvider.GetRequiredService<QdratNew.Services.OnlineLecture.ILectureContentAdminService>();
                Assert.True((await admin.SetBatchOnlineLearningAsync(_seed.BatchA1, enable: false)).Success);
            }

            var (_, off) = await GetAsync(client, "/Students/OnlineLearning");
            Assert.DoesNotContain(link, off);
            Assert.Contains("المحتوى التعليمي غير متاح لدورتك الحالية", off);

            using (var scope = _factory.Services.CreateScope())
            {
                var admin = scope.ServiceProvider.GetRequiredService<QdratNew.Services.OnlineLecture.ILectureContentAdminService>();
                Assert.True((await admin.SetBatchOnlineLearningAsync(_seed.BatchA1, enable: true)).Success);
            }

            var (_, on) = await GetAsync(client, "/Students/OnlineLearning");
            Assert.Contains(link, on);
        }

        // ───────────── OLC-S6: المشغّل المحمي ─────────────

        private async Task<(HttpStatusCode Status, string Html, string? Location)> GetRawAsync(HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            return (response.StatusCode, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), response.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task Watch_OpenFullVideo_RendersProtectedPlayer_WithServerWatermark_AndNoStore()
        {
            var client = StudentClient(_studentAUser);
            var response = await client.GetAsync($"/Students/OnlineLearning/Watch/{_content1}");
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);

            var who = await WithDbAsync(db => db.Students.AsNoTracking()
                .Where(s => s.StudentID == _seed.ActiveStudentA).Select(s => new { s.FullName, s.NationalID }).FirstAsync());
            Assert.Contains(who.FullName, html);                         // P1
            Assert.Contains(who.NationalID.Trim(), html);
            Assert.Contains("olc-watermark-a", html);
            Assert.Contains("olc-watermark-b", html);
            Assert.Contains("olcPlayerShield", html);                    // P2
            Assert.Contains("online-lecture-player.js", html);
            Assert.Contains($"data-external-id=\"{FullVideoId}\"", html); // المحاضرة المفتوحة فقط
            Assert.Contains($"https://www.youtube-nocookie.com/embed/{FullVideoId}", html);   // R8 من الخادم
            Assert.DoesNotContain(ItemVideoId, html);                    // P6: المحاضرة المقفلة لا تُكشف
            Assert.DoesNotContain("وضع المراجعة", html);
        }

        [Fact]
        public async Task Watch_LockedLecture_RedirectsWithMessage_AndLeaksNoSource()
        {
            var client = StudentClient(_studentAUser);

            var (status, html, location) = await GetRawAsync(client, $"/Students/OnlineLearning/Watch/{_content2}");
            Assert.Equal(HttpStatusCode.Redirect, status);                // R15
            Assert.Equal("/Students/OnlineLearning", location);
            Assert.DoesNotContain(ItemVideoId, html);
            Assert.DoesNotContain("vimeo", html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Watch_AfterCompletion_PreviousInReview_NextOpensWithItemSource()
        {
            await DbAsync(async db =>
            {
                db.LectureContentProgress.Add(new LectureContentProgress
                {
                    LectureContentId = _content1, StudentId = _seed.ActiveStudentA, WatchedSeconds = 600, MaxPositionSeconds = 600,
                    DurationSeconds = 600, EndedSeen = true, IsCompleted = true, CompletedAtUtc = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            });
            var client = StudentClient(_studentAUser);

            var (status, review) = await GetAsync(client, $"/Students/OnlineLearning/Watch/{_content1}");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Contains("وضع المراجعة", review);                       // P11
            Assert.Contains("data-review=\"1\"", review);
            Assert.Contains($"/Students/OnlineLearning/Watch/{_content2}", review);   // المحاضرة التالية مفتوحة

            (status, var items) = await GetAsync(client, $"/Students/OnlineLearning/Watch/{_content2}");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Contains("olcItemList", items);
            Assert.Contains($"data-external-id=\"{ItemVideoId}\"", items);
            Assert.Contains($"https://player.vimeo.com/video/{ItemVideoId}", items);
        }

        [Fact]
        public async Task Watch_OtherBatchContent_Withdrawn_AndFeatureOff_Return404()
        {
            var otherBatchContent = await WithDbAsync(db => db.LectureContents.AsNoTracking()
                .Where(c => c.Title == "OLCS5 محتوى دفعة B").Select(c => c.Id).FirstAsync());

            var (status, _) = await GetAsync(StudentClient(_studentAUser), $"/Students/OnlineLearning/Watch/{otherBatchContent}");
            Assert.Equal(HttpStatusCode.NotFound, status);                 // IDOR: دفعة أخرى
            (status, _) = await GetAsync(StudentClient(_withdrawnUser), $"/Students/OnlineLearning/Watch/{_content1}");
            Assert.Equal(HttpStatusCode.NotFound, status);                 // تسجيل غير نشط
            (status, _) = await GetAsync(StudentClient(_studentAUser), "/Students/OnlineLearning/Watch/999999999");
            Assert.Equal(HttpStatusCode.NotFound, status);

            await DbAsync(db => db.SystemSettings.Where(s => s.Key == FeatureKey)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, "false")));
            ResetCaches();
            (status, _) = await GetAsync(StudentClient(_studentAUser), $"/Students/OnlineLearning/Watch/{_content1}");
            Assert.Equal(HttpStatusCode.NotFound, status);
            await DbAsync(db => db.SystemSettings.Where(s => s.Key == FeatureKey)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, "true")));
            ResetCaches();
        }

        [Fact]
        public async Task Index_ShowsWatchLinks_ForOpenLecturesOnly()
        {
            var (status, html) = await GetAsync(StudentClient(_studentAUser), "/Students/OnlineLearning");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Contains($"href=\"/Students/OnlineLearning/Watch/{_content1}\"", html);
            Assert.DoesNotContain($"/Students/OnlineLearning/Watch/{_content2}", html);   // المقفلة بلا رابط
        }

        // ───────────── OLC-S7.1: النبضة عبر HTTP (AntiForgery + R2/R13 + التحديث الذرّي على SQL Server) ─────────────

        private async Task<(HttpStatusCode Status, System.Text.Json.JsonElement Body)> PingAsync(
            string userId, object body, bool withToken = true)
        {
            var client = StudentClient(userId);
            var request = new HttpRequestMessage(HttpMethod.Post, "/Students/OnlineLearning/Ping")
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
            };
            if (withToken)
            {
                var (cookie, token) = _factory.CreateValidAntiForgeryTokens(userId);
                request.Headers.Add("Cookie", cookie);
                request.Headers.Add("RequestVerificationToken", token);
            }
            var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            var json = string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith("{")
                ? default
                : System.Text.Json.JsonDocument.Parse(text).RootElement.Clone();
            return (response.StatusCode, json);
        }

        [Fact]
        public async Task Ping_RejectsWithoutAntiForgery_LockedIs409_OtherBatchIs404_WithoutWrites()
        {
            var (status, _) = await PingAsync(_studentAUser, new { lectureContentId = _content1, state = "playing", position = 0, duration = 600 }, withToken: false);
            Assert.Equal(HttpStatusCode.BadRequest, status);                               // CSRF

            (status, var locked) = await PingAsync(_studentAUser, new { lectureContentId = _content2, itemId = 1, state = "playing", position = 0, duration = 100 });
            Assert.Equal(HttpStatusCode.Conflict, status);                                 // R13 على الخادم
            Assert.Equal("locked", locked.GetProperty("reason").GetString());

            var otherBatchContent = await WithDbAsync(db => db.LectureContents.AsNoTracking()
                .Where(c => c.Title == "OLCS5 محتوى دفعة B").Select(c => c.Id).FirstAsync());
            (status, var missing) = await PingAsync(_studentAUser, new { lectureContentId = otherBatchContent, state = "playing", position = 0, duration = 600 });
            Assert.Equal(HttpStatusCode.NotFound, status);                                 // IDOR
            Assert.Equal("notfound", missing.GetProperty("reason").GetString());

            (status, _) = await PingAsync(_withdrawnUser, new { lectureContentId = _content1, state = "playing", position = 0, duration = 600 });
            Assert.Equal(HttpStatusCode.NotFound, status);

            var rows = await WithDbAsync(db => db.LectureContentProgress.AsNoTracking().CountAsync(p => p.LectureContentId == _content1));
            var itemRows = await WithDbAsync(db => db.LectureContentItemProgress.AsNoTracking()
                .CountAsync(p => p.LectureContentItem.LectureContentId == _content2));
            Assert.Equal(0, rows);
            Assert.Equal(0, itemRows);
        }

        [Fact]
        public async Task Ping_RecordsServerTimedProgress_ThenCompletion_UnlocksNextLecture()
        {
            // أول نبضة تُنشئ الصف، والثانية تمر على ExecuteUpdate بشرط LastPingAtUtc
            var (status, first) = await PingAsync(_studentAUser, new { lectureContentId = _content1, state = "playing", position = 0, duration = 600 });
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.True(first.GetProperty("ok").GetBoolean());
            Assert.Equal(480, first.GetProperty("requiredSeconds").GetInt32());

            (status, var paused) = await PingAsync(_studentAUser, new { lectureContentId = _content1, state = "paused", position = 590, duration = 600 });
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.False(paused.GetProperty("completed").GetBoolean());

            var row = await WithDbAsync(db => db.LectureContentProgress.AsNoTracking()
                .SingleAsync(p => p.LectureContentId == _content1 && p.StudentId == _seed.ActiveStudentA));
            Assert.True(row.MaxPositionSeconds < 10);                                      // P4: الموضع المزيّف لا يُقبل
            Assert.Equal("paused", row.LastPingState);
            Assert.NotNull(row.UpdatedAt);

            // محاكاة مشاهدة طويلة سابقة: آخر نبضة playing قبل 15ث ورصيد 470ث
            var lastPing = DateTime.UtcNow.AddSeconds(-15);
            await DbAsync(db => db.LectureContentProgress.Where(p => p.Id == row.Id).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.WatchedSeconds, 470)
                .SetProperty(p => p.MaxPositionSeconds, 588)
                .SetProperty(p => p.LastPingState, "playing")
                .SetProperty(p => p.LastPingAtUtc, (DateTime?)lastPing)));

            (status, var done) = await PingAsync(_studentAUser, new { lectureContentId = _content1, state = "ended", position = 600, duration = 600 });
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.True(done.GetProperty("completed").GetBoolean());
            Assert.True(done.GetProperty("lectureCompleted").GetBoolean());
            Assert.True(done.GetProperty("nextLectureUnlocked").GetBoolean());

            // الخادم فتح التالية فعلًا (R15) ونبضة الإعادة idempotent
            var (watchStatus, _) = await GetAsync(StudentClient(_studentAUser), $"/Students/OnlineLearning/Watch/{_content2}");
            Assert.Equal(HttpStatusCode.OK, watchStatus);
            (status, var again) = await PingAsync(_studentAUser, new { lectureContentId = _content1, state = "playing", position = 10, duration = 600 });
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.True(again.GetProperty("completed").GetBoolean());
        }

        private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
        {
            using var scope = _factory.Services.CreateScope();
            return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        [Fact]
        public async Task FeatureOff_Returns404_AndHidesMenuLink()
        {
            await DbAsync(db => db.SystemSettings.Where(s => s.Key == FeatureKey)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, "false")));
            ResetCaches();

            var client = StudentClient(_studentAUser);
            var (status, _) = await GetAsync(client, "/Students/OnlineLearning");
            Assert.Equal(HttpStatusCode.NotFound, status);

            var (dashStatus, dashboard) = await GetAsync(client, "/Students/RemedialTrack");
            if (dashStatus == HttpStatusCode.OK)
                Assert.DoesNotContain("href=\"/Students/OnlineLearning\"", dashboard);

            await DbAsync(db => db.SystemSettings.Where(s => s.Key == FeatureKey)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, "true")));
            ResetCaches();
        }
    }
}
