using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace QdratNew.Tests.Integration
{
    // RTK-S7.3 — اختبار حمل نبضات الفيديو: 500 طالب افتراضي × نبضة/15 ثانية × 5 دقائق على قاعدة الاختبار المعزولة (حسابات اصطناعية).
    // k6 غير متاح على جهاز التطوير، فالبديل تطبيق Program.cs الحقيقي داخل العملية (TestServer + مصادقة الترويسات) مع SQL Server حقيقي.
    // تنبيه منهجي: مولّد الحمل والخادم في عملية واحدة (يتنافسان على المعالج) ولا شبكة بينهما — فالأرقام تحفّظية للخادم وتخلو من زمن الشبكة.
    // مدة كل استعلام تُقاس على الخادم عبر Interceptor للأوامر (DbCommandInterceptor) لا من العميل.
    //
    // اختياري (مكلف): يتطلب QDRAT_TEST_SQL + RTK_LOAD=1. ضبط اختياري: RTK_LOAD_STUDENTS (500) / RTK_LOAD_SECONDS (300) / RTK_LOAD_INTERVAL (15).
    // الأهداف: p95 < 300ms، 0 أخطاء 5xx (وأي استجابة غير 200)، p95 لأي استعلام < 50ms.
    public sealed class RtkLoadFactAttribute : FactAttribute
    {
        public RtkLoadFactAttribute()
        {
            if (!RtkRealDb.IsConfigured)
                Skip = $"متغير البيئة {SqlServerFactAttribute.EnvName} غير مضبوط.";
            else if (Environment.GetEnvironmentVariable("RTK_LOAD") != "1")
                Skip = "اختبار الحمل اختياري: اضبط RTK_LOAD=1 لتشغيله.";
        }
    }

    public sealed class SqlTimingInterceptor : DbCommandInterceptor
    {
        public static readonly ConcurrentBag<(string Kind, double Ms)> Samples = new();

        private static void Record(Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData e)
        {
            var text = e.Command.CommandText;
            if (!text.Contains("RemedialTrack", StringComparison.Ordinal)) return;
            var kind = text.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ? "UPDATE"
                     : text.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ? "INSERT" : "SELECT";
            Samples.Add((kind, e.Duration.TotalMilliseconds));
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken ct = default)
        { Record(eventData); return base.ReaderExecutedAsync(command, eventData, result, ct); }

        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken ct = default)
        { Record(eventData); return base.NonQueryExecutedAsync(command, eventData, result, ct); }
    }

    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "Load")]
    public sealed class RemedialTrackLoadTests : IClassFixture<RemedialTrackLoadTests.LoadFactory>, IAsyncLifetime
    {
        public sealed class LoadFactory : CustomWebApplicationFactory
        {
            protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);
                // interceptors المسجَّلة في DI لا تُلتقط تلقائيًا في EF 8 مع AddDbContext((sp, o) => ...)؛ فنعيد تسجيل الخيارات بنفس إعداد
                // Program.cs (SQL Server + CompatibilityLevel 120 + RetryOnFailure + interceptor الكاش الأصلي) مع إضافة مؤقّت الاستعلامات.
                builder.ConfigureServices(s =>
                {
                    var timing = new SqlTimingInterceptor();
                    DbContextOptions<QdratNew.Data.ApplicationDbContext> Build(IServiceProvider sp)
                        => new DbContextOptionsBuilder<QdratNew.Data.ApplicationDbContext>()
                            .UseSqlServer(Microsoft.Extensions.Configuration.ConfigurationExtensions.GetConnectionString(sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(), "DefaultConnection"),
                                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery).UseCompatibilityLevel(120).EnableRetryOnFailure().CommandTimeout(180))
                            .AddInterceptors(timing, sp.GetRequiredService<QdratNew.Data.Interceptors.HomePageCacheInvalidationInterceptor>())
                            .Options;

                    s.Replace(ServiceDescriptor.Scoped<DbContextOptions<QdratNew.Data.ApplicationDbContext>>(Build));
                    s.Replace(ServiceDescriptor.Scoped<DbContextOptions>(sp => sp.GetRequiredService<DbContextOptions<QdratNew.Data.ApplicationDbContext>>()));
                });
            }
        }

        private readonly LoadFactory _factory;
        private readonly ITestOutputHelper _out;

        public RemedialTrackLoadTests(LoadFactory factory, ITestOutputHelper output)
        {
            _factory = factory;
            _out = output;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task DisposeAsync() => RtkRealDb.IsConfigured ? RtkRealDb.CleanupAsync() : Task.CompletedTask;

        private static int EnvInt(string name, int def) => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v > 0 ? v : def;

        private static double Percentile(IReadOnlyList<double> sorted, double p)
            => sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(p / 100.0 * sorted.Count) - 1)];

        [RtkLoadFact]
        public async Task VideoPings_500Students_Every15s_For5Minutes_MeetsTargets()
        {
            var students = EnvInt("RTK_LOAD_STUDENTS", 500);
            var seconds = EnvInt("RTK_LOAD_SECONDS", 300);
            var interval = EnvInt("RTK_LOAD_INTERVAL", 15);

            // فيديوهات كثيرة بحيث لا يُتمّ أي طالب المحور خلال المدة (الحمل المستدام هو المقصود)
            var seed = await RtkRealDb.SeedAsync(students, videosPerAxis: 8, questionsPerModel: 2);
            var seededAt = DateTime.UtcNow;
            SqlTimingInterceptor.Samples.Clear();

            // تهيئة/تسخين التطبيق (JIT + اتصال أول) قبل بدء القياس
            using (var warm = _factory.CreateClient())
                await warm.GetAsync("/LMS/login");

            var latencies = new ConcurrentBag<double>();
            var statuses = new ConcurrentDictionary<int, int>();
            var errors = new ConcurrentBag<string>();

            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            var started = Stopwatch.StartNew();

            async Task VirtualStudentAsync(int index)
            {
                var s = seed.Students[index];
                var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
                var t = _factory.CreateValidAntiForgeryTokens(s.UserId);
                client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, s.UserId);
                client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Student");
                client.DefaultRequestHeaders.Add("Cookie", t.CookieHeader);
                client.DefaultRequestHeaders.Add("RequestVerificationToken", t.RequestToken);

                // توزيع بدايات الطلاب على نافذة الفاصل كما يحدث فعليًا (لا موجة متزامنة)
                await Task.Delay(TimeSpan.FromSeconds(interval * (index / (double)students)));

                var current = 0;
                var position = 0;
                while (DateTime.UtcNow < deadline)
                {
                    var body = JsonSerializer.Serialize(new
                    {
                        EnrollmentId = s.EnrollmentId, VideoProgressId = s.FirstAxisVideoProgressIds[current],
                        State = "playing", Position = position, Duration = 100
                    });
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        var r = await client.PostAsync("/Students/RemedialTrack/VideoPing", new StringContent(body, Encoding.UTF8, "application/json"));
                        var text = await r.Content.ReadAsStringAsync();
                        sw.Stop();
                        latencies.Add(sw.Elapsed.TotalMilliseconds);
                        statuses.AddOrUpdate((int)r.StatusCode, 1, (_, n) => n + 1);

                        if (r.StatusCode == HttpStatusCode.OK)
                        {
                            using var doc = JsonDocument.Parse(text);
                            if (doc.RootElement.TryGetProperty("Completed", out var c) && c.ValueKind == JsonValueKind.True
                                && current + 1 < s.FirstAxisVideoProgressIds.Length)
                            {
                                current++;
                                position = 0;
                            }
                        }
                        else errors.Add($"{(int)r.StatusCode}: {(text.Length > 120 ? text[..120] : text)}");
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex.GetType().Name + ": " + ex.Message);
                    }

                    position += interval;
                    var wait = TimeSpan.FromSeconds(interval) - sw.Elapsed;
                    if (wait > TimeSpan.Zero) await Task.Delay(wait);
                }
            }

            await Task.WhenAll(Enumerable.Range(0, students).Select(i => Task.Run(() => VirtualStudentAsync(i))));
            started.Stop();

            var lat = latencies.OrderBy(x => x).ToList();
            var sql = SqlTimingInterceptor.Samples.ToArray();
            string SqlLine(string kind)
            {
                var d = sql.Where(x => x.Kind == kind).Select(x => x.Ms).OrderBy(x => x).ToList();
                return d.Count == 0 ? $"{kind}: لا عينات"
                    : $"{kind}: n={d.Count} avg={d.Average():0.0}ms p95={Percentile(d, 95):0.0}ms p99={Percentile(d, 99):0.0}ms max={d[^1]:0.0}ms";
            }

            var total = lat.Count;
            var report = new StringBuilder()
                .AppendLine($"RTK-S7.3 load report — {DateTime.UtcNow:u}")
                .AppendLine($"students={students} interval={interval}s duration={started.Elapsed.TotalSeconds:0}s videosPerAxis=8")
                .AppendLine($"requests={total} rps≈{total / started.Elapsed.TotalSeconds:0.0}")
                .AppendLine($"latency(client, in-process): avg={(lat.Count == 0 ? 0 : lat.Average()):0.0}ms p50={Percentile(lat, 50):0.0}ms p95={Percentile(lat, 95):0.0}ms p99={Percentile(lat, 99):0.0}ms max={(lat.Count == 0 ? 0 : lat[^1]):0.0}ms")
                .AppendLine("status codes: " + string.Join(", ", statuses.OrderBy(k => k.Key).Select(k => $"{k.Key}×{k.Value}")))
                .AppendLine(SqlLine("SELECT")).AppendLine(SqlLine("UPDATE")).AppendLine(SqlLine("INSERT"))
                .AppendLine($"errors={errors.Count}" + (errors.IsEmpty ? "" : " e.g. " + string.Join(" | ", errors.Take(3))))
                .ToString();
            _out.WriteLine(report);
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "rtk-load-report.txt"), report, Encoding.UTF8);

            Assert.True(total >= students * (seconds / interval) * 0.9, $"عدد الطلبات أقل من المتوقع بكثير: {total}\n{report}");
            Assert.True(errors.IsEmpty, "توجد استجابات غير 200/أخطاء:\n" + report);
            Assert.DoesNotContain(statuses.Keys, k => k >= 500);
            Assert.True(Percentile(lat, 95) < 300, "p95 ≥ 300ms\n" + report);
            Assert.True(sql.Length > 0, "لم تُلتقط أي عينة SQL (الـ Interceptor غير مُفعَّل)\n" + report);
            var allSql = sql.Select(x => x.Ms).OrderBy(x => x).ToList();
            Assert.True(Percentile(allSql, 95) < 50, "p95 لاستعلام SQL ≥ 50ms\n" + report);
        }
    }
}
