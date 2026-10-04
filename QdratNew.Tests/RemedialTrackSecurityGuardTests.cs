using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Filters;
using QdratNew.Security;
using QdratNew.Services.Interfaces;
using QdratNew.Services.RemedialTracks;
using Xunit;

namespace QdratNew.Tests
{
    // RTK-S7.1 — حارس أمني آلي لكل Endpoints الخطة العلاجية (القائمة 11.1) يمنع التراجع مستقبلًا.
    // لا يحتاج قاعدة بيانات: يفحص بنية الكنترولرات والـ Views وقوالب السجلات + مفتاح التعطيل (S7.4).
    public class RemedialTrackSecurityGuardTests
    {
        private static readonly Type StudentController = typeof(Areas.Students.Controllers.RemedialTrackController);

        private static readonly Type[] AdminControllers =
        {
            typeof(Areas.Admin.Controllers.RemedialTracksController),
            typeof(Areas.Admin.Controllers.RemedialTrackPublicationsController),
            typeof(Areas.Admin.Controllers.RemedialTrackReportsController)
        };

        private static IEnumerable<Type> AllControllers => AdminControllers.Append(StudentController);

        private static IEnumerable<MethodInfo> ActionsOf(Type controller)
            => controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);

        private static bool IsPost(MethodInfo m) => m.GetCustomAttribute<HttpPostAttribute>() is not null;

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QdratNew.csproj"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("QdratNew.csproj غير موجود فوق مجلد الاختبارات.");
        }

        [Fact]
        public void AllControllers_ExposeActions() => Assert.All(AllControllers, c => Assert.NotEmpty(ActionsOf(c)));

        [Fact]
        public void EveryPostAction_HasValidateAntiForgeryToken()
        {
            var missing = AllControllers.SelectMany(ActionsOf).Where(IsPost)
                .Where(m => m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is null)
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}").ToList();

            Assert.True(missing.Count == 0, "POST بلا ValidateAntiForgeryToken: " + string.Join(", ", missing));
        }

        [Fact]
        public void NoController_OptsOutOfAntiforgery_OrAuthorization()
        {
            var offenders = new List<string>();
            foreach (var c in AllControllers)
            {
                if (c.GetCustomAttributes(true).Any(a => a is IgnoreAntiforgeryTokenAttribute or AllowAnonymousAttribute))
                    offenders.Add(c.Name);
                foreach (var m in ActionsOf(c))
                    if (m.GetCustomAttributes(true).Any(a => a is IgnoreAntiforgeryTokenAttribute or AllowAnonymousAttribute))
                        offenders.Add($"{c.Name}.{m.Name}");
            }

            Assert.True(offenders.Count == 0, "IgnoreAntiforgeryToken/AllowAnonymous ممنوعان: " + string.Join(", ", offenders));
        }

        [Fact]
        public void EveryAction_HasExplicitHttpVerb_SoNoStateChangeIsReachableByGet()
        {
            var missing = AllControllers.SelectMany(ActionsOf)
                .Where(m => m.GetCustomAttribute<HttpGetAttribute>() is null && !IsPost(m))
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}").ToList();

            Assert.True(missing.Count == 0, "أكشن بلا فعل HTTP صريح: " + string.Join(", ", missing));
        }

        [Fact]
        public void EveryAdminAction_RequiresItsOwnRemedialTrackPermission()
        {
            var expectedPrefix = new Dictionary<Type, string>
            {
                [typeof(Areas.Admin.Controllers.RemedialTracksController)] = "RemedialTracks:",
                [typeof(Areas.Admin.Controllers.RemedialTrackPublicationsController)] = "RemedialTrackPublications:",
                [typeof(Areas.Admin.Controllers.RemedialTrackReportsController)] = "RemedialTrackReports:"
            };

            var bad = AdminControllers.SelectMany(c => ActionsOf(c).Select(m => (c, m)))
                .Where(x =>
                {
                    var perm = x.m.GetCustomAttribute<AdminPermissionAttribute>();
                    return perm?.Policy is null || !perm.Policy.StartsWith(expectedPrefix[x.c], StringComparison.Ordinal);
                })
                .Select(x => $"{x.c.Name}.{x.m.Name}").ToList();

            Assert.True(bad.Count == 0, "أكشن أدمن بلا صلاحية RemedialTrack*: " + string.Join(", ", bad));
        }

        [Fact]
        public void EveryAdminMutation_RequiresAWritePermission_NotRead()
        {
            var weak = AdminControllers.SelectMany(ActionsOf).Where(IsPost)
                .Where(m => m.GetCustomAttribute<AdminPermissionAttribute>()!.Policy!.EndsWith(":Read", StringComparison.Ordinal))
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}").ToList();

            Assert.True(weak.Count == 0, "أكشن POST بصلاحية Read فقط: " + string.Join(", ", weak));
        }

        [Fact]
        public void StudentController_IsStudentOnly_AndGatedByKillSwitch()
        {
            var auth = StudentController.GetCustomAttributes<AuthorizeAttribute>(true).ToList();
            Assert.Contains(auth, a => a.Roles == "Student");

            var gated = StudentController.GetCustomAttributes<ServiceFilterAttribute>(true)
                .Any(a => a.ServiceType == typeof(RemedialTrackEnabledFilter));
            Assert.True(gated, "مفتاح التعطيل RemedialTrack.Enabled غير مطبَّق على كنترولر الطالب");
        }

        [Fact]
        public void StudentActions_NeverAcceptStudentIdFromClient_IdorGuard()
        {
            var offenders = ActionsOf(StudentController)
                .SelectMany(m => m.GetParameters().Select(p => (Action: m.Name, p.Name)))
                .Where(x => x.Name!.Contains("studentId", StringComparison.OrdinalIgnoreCase))
                .Select(x => $"{x.Action}({x.Name})").ToList();

            Assert.True(offenders.Count == 0, "الطالب يُستمد من الجلسة لا من العميل: " + string.Join(", ", offenders));
        }

        [Theory]
        [InlineData("VerifyCode", "rtk-code")]
        [InlineData("VideoPing", "rtk-ping")]
        [InlineData("SaveAnswer", "rtk-exam")]
        public void SensitiveStudentEndpoints_HaveRateLimitingPolicy(string action, string policy)
        {
            var attr = StudentController.GetMethod(action)!.GetCustomAttribute<EnableRateLimitingAttribute>();
            Assert.NotNull(attr);
            Assert.Equal(policy, attr!.PolicyName);
        }

        [Fact]
        public void NoAction_BindsEntityDirectly_OverpostingGuard()
        {
            var offenders = AllControllers.SelectMany(ActionsOf)
                .SelectMany(m => m.GetParameters().Select(p => (Action: $"{m.DeclaringType!.Name}.{m.Name}", p)))
                .Where(x => x.p.ParameterType.Namespace == "QdratNew.Entities")
                .Select(x => $"{x.Action}({x.p.ParameterType.Name} {x.p.Name})").ToList();

            Assert.True(offenders.Count == 0, "أكشن يقبل Entity مباشرة: " + string.Join(", ", offenders));
        }

        [Fact]
        public void RemedialTrackViews_NeverRenderRawHtml_XssGuard()
        {
            var root = RepoRoot();
            var dirs = new[]
            {
                Path.Combine(root, "Areas", "Students", "Views", "RemedialTrack"),
                Path.Combine(root, "Areas", "Admin", "Views", "RemedialTracks"),
                Path.Combine(root, "Areas", "Admin", "Views", "RemedialTrackPublications"),
                Path.Combine(root, "Areas", "Admin", "Views", "RemedialTrackReports")
            };

            var files = dirs.Where(Directory.Exists).SelectMany(d => Directory.GetFiles(d, "*.cshtml", SearchOption.AllDirectories)).ToList();
            Assert.NotEmpty(files);

            var offenders = files.Where(f => Regex.IsMatch(File.ReadAllText(f), @"Html\.Raw|new\s+HtmlString|IHtmlContent"))
                .Select(f => Path.GetFileName(f)!).ToList();
            Assert.True(offenders.Count == 0, "Html.Raw ممنوع في Views الخطة العلاجية: " + string.Join(", ", offenders));
        }

        [Fact]
        public void StudentViews_NeverReferenceCorrectAnswer_OrAccessCode()
        {
            var dir = Path.Combine(RepoRoot(), "Areas", "Students", "Views", "RemedialTrack");
            var offenders = Directory.GetFiles(dir, "*.cshtml")
                .Where(f => Regex.IsMatch(File.ReadAllText(f), @"CorrectAnswer|AccessCode|\.Explanation"))
                .Select(f => Path.GetFileName(f)!).ToList();

            Assert.True(offenders.Count == 0, "Views الطالب تلمس CorrectAnswer/AccessCode/Explanation: " + string.Join(", ", offenders));
        }

        [Fact]
        public void ParentReportViews_NeverShowReferenceCode()
        {
            var dir = Path.Combine(RepoRoot(), "Areas", "Admin", "Views", "RemedialTrackReports");
            var offenders = Directory.GetFiles(dir, "*.cshtml")
                .Where(f => Regex.IsMatch(File.ReadAllText(f), @"AccessCode|CodeVersion|ReferenceCode"))
                .Select(f => Path.GetFileName(f)!).ToList();

            Assert.True(offenders.Count == 0, "تقرير ولي الأمر/الدفعة يعرض الرقم المرجعي: " + string.Join(", ", offenders));
        }

        [Fact]
        public void Services_NeverLogAccessCodesOrAnswers()
        {
            var dir = Path.Combine(RepoRoot(), "Services", "RemedialTracks");
            var logCall = new Regex(@"_logger\.Log\w+\((?<args>[^;]*)\);", RegexOptions.Singleline);
            var offenders = new List<string>();
            foreach (var f in Directory.GetFiles(dir, "*.cs"))
                foreach (Match m in logCall.Matches(File.ReadAllText(f)))
                    if (Regex.IsMatch(m.Groups["args"].Value, @"AccessCode|\{Code\}|\{Answer\}|input\.Code|\{Selected", RegexOptions.IgnoreCase))
                        offenders.Add($"{Path.GetFileName(f)}: {m.Value.Trim()}");

            Assert.True(offenders.Count == 0, "سجل يحوي الرقم المرجعي/الإجابات: " + string.Join(" | ", offenders));
        }

        // ───────────── مفتاح التعطيل (S7.4) ─────────────

        private sealed class FakeSettings : ISystemSettingService
        {
            public string? Value { get; set; }
            public int Reads { get; private set; }
            public Task<string?> GetAsync(string key) { Reads++; return Task.FromResult(Value); }
            public string? GetValue(string key) => Value;
            public int GetInt(string key, int defaultValue = 0) => defaultValue;
            public bool GetBool(string key, bool defaultValue = false) => defaultValue;
            public Task<bool> UpdateValueAsync(string key, string value) => Task.FromResult(true);
            public Task<int> GetIntAsync(string key, int defaultValue = 0) => Task.FromResult(defaultValue);
            public Task<double> GetDoubleAsync(string key, double defaultValue = 0) => Task.FromResult(defaultValue);
            public Task<bool> GetBoolAsync(string key, bool defaultValue = false) => Task.FromResult(defaultValue);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("garbage", true)]
        [InlineData("true", true)]
        [InlineData("True", true)]
        [InlineData("false", false)]
        [InlineData(" FALSE ", false)]
        public void FeatureFlag_DefaultsToEnabled_OnlyExplicitFalseDisables(string? raw, bool expected)
            => Assert.Equal(expected, RemedialTrackFeatureService.Parse(raw));

        [Fact]
        public async Task FeatureService_CachesBriefly_ThenReflectsChange()
        {
            var settings = new FakeSettings { Value = "true" };
            var cache = new MemoryCache(new MemoryCacheOptions());
            var svc = new RemedialTrackFeatureService(settings, cache);

            Assert.True(await svc.IsEnabledAsync());
            settings.Value = "false";
            Assert.True(await svc.IsEnabledAsync());      // من الكاش
            Assert.Equal(1, settings.Reads);

            cache.Remove("rtk-feature-enabled");
            Assert.False(await svc.IsEnabledAsync());
        }

        private sealed class FakeFeature : IRemedialTrackFeatureService
        {
            public bool Enabled { get; set; }
            public Task<bool> IsEnabledAsync(CancellationToken ct = default) => Task.FromResult(Enabled);
        }

        private static (ActionExecutingContext Ctx, ActionExecutionDelegate Next, Func<bool> Called) FilterCtx(Action<HttpRequest>? configure = null)
        {
            var http = new DefaultHttpContext();
            configure?.Invoke(http.Request);
            var actionCtx = new ActionContext(http, new RouteData(), new ActionDescriptor());
            var exec = new ActionExecutingContext(actionCtx, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
            var called = false;
            ActionExecutionDelegate next = () =>
            {
                called = true;
                return Task.FromResult(new ActionExecutedContext(actionCtx, new List<IFilterMetadata>(), new object()));
            };
            return (exec, next, () => called);
        }

        [Fact]
        public async Task Filter_WhenEnabled_RunsAction()
        {
            var (ctx, next, called) = FilterCtx();
            await new RemedialTrackEnabledFilter(new FakeFeature { Enabled = true }).OnActionExecutionAsync(ctx, next);

            Assert.True(called());
            Assert.Null(ctx.Result);
        }

        [Fact]
        public async Task Filter_WhenDisabled_Html_Returns404MaintenanceView()
        {
            var (ctx, next, called) = FilterCtx();
            await new RemedialTrackEnabledFilter(new FakeFeature { Enabled = false }).OnActionExecutionAsync(ctx, next);

            Assert.False(called());
            var view = Assert.IsType<ViewResult>(ctx.Result);
            Assert.Equal("Maintenance", view.ViewName);
            Assert.Equal(404, view.StatusCode);
        }

        [Fact]
        public async Task Filter_WhenDisabled_Json_Returns404JsonBody()
        {
            var (ctx, next, called) = FilterCtx(r => r.ContentType = "application/json");
            await new RemedialTrackEnabledFilter(new FakeFeature { Enabled = false }).OnActionExecutionAsync(ctx, next);

            Assert.False(called());
            var obj = Assert.IsType<ObjectResult>(ctx.Result);
            Assert.Equal(404, obj.StatusCode);
        }
    }
}
