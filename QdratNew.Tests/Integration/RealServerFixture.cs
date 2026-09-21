using System.Diagnostics;
using System.Net.Http;

namespace QdratNew.Tests.Integration
{
    // Sprint 9 (QG-G / G3) — يُشغّل QdratNew.dll الحقيقي كعملية فرعية مستقلة (Kestrel حقيقي على
    // منفذ فعلي) بدل WebApplicationFactory: تبيّن أن WebApplicationFactory<Program> الأساسية في
    // Microsoft.AspNetCore.Mvc.Testing 8.0.5 تفترض داخليًا TestServer وترمي استثناء صريح
    // (InvalidCastException) عند تجاوز CreateHost لاستخدام Kestrel حقيقي — وهو مطلوب هنا لأن
    // متصفح Playwright الفعلي يحتاج عنوان HTTP حقيقي (TestServer داخل الذاكرة لا يصلح). تشغيل
    // العملية الحقيقية مباشرة أبسط وأقرب للواقع، ويتجنّب هذا القيد تمامًا.
    internal sealed class RealServerFixture : IAsyncDisposable
    {
        public string BaseUrl { get; }
        private readonly Process _process;

        private RealServerFixture(Process process, string baseUrl)
        {
            _process = process;
            BaseUrl = baseUrl;
        }

        public static async Task<RealServerFixture> StartAsync()
        {
            await TestDatabaseBaseline.EnsureAsync();

            const int port = 5299;
            var baseUrl = $"http://127.0.0.1:{port}";

            var dllPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "bin", "Debug", "net8.0", "QdratNew.dll"));
            if (!File.Exists(dllPath))
                throw new FileNotFoundException("لم يُعثَر على QdratNew.dll المبني — شغّل dotnet build على المشروع الرئيسي أولًا.", dllPath);

            // WorkingDirectory = جذر المشروع المصدري نفسه (وليس مجلد bin) — Program.cs يستخدم
            // app.Environment.WebRootPath (مجلد wwwroot) وهو موجود فقط في جذر المشروع، لا يُنسَخ
            // إلى bin عند dotnet build عادي (بلا publish).
            var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            // ملاحظة معمارية أخرى مكتشَفة أثناء هذا الـ Sprint: هذا المشروع يضبط
            // RazorCompileOnBuild=false في تهيئة Debug (يعتمد على RazorRuntimeCompilation، وهي
            // مُفعَّلة فقط عند IsDevelopment() في Program.cs) — فتصبح صفحات Razor (خصوصًا Razor
            // Pages مثل /LMS/login) غير قابلة للعرض إطلاقًا (404/500) تحت أي بيئة غير Development
            // ما لم تُبنَ Release. جرّبنا بناء Release فعليًا فاصطدم بعيب مسبق آخر غير متعلق
            // بحماية الترجمة: CS8103 (تجاوز الحد الأقصى لطول السلاسل النصية المجمّعة) بسبب حجم
            // Views الكبير في هذا المشروع — عيب بناء Release قائم مسبقًا خارج نطاق هذا الـ Sprint
            // تمامًا. الحل العملي: تشغيل العملية الفرعية ببيئة "Development" (لتفعيل
            // RazorRuntimeCompilation فقط) مع فرض سلسلة اتصال قاعدة الاختبار المعزولة صراحةً عبر
            // معامل سطر أوامر — فتبقى خاصية العزل الكاملة عن قاعدة QdratNewDB الحقيقية قائمة كما
            // هي، وتُستخدَم Development فقط لتشغيل الـ Views لا لأي غرض آخر.
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{dllPath}\"",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            startInfo.Environment["ASPNETCORE_URLS"] = baseUrl;
            // Program.cs يعيد إضافة appsettings.{Environment}.json ثم AddEnvironmentVariables()
            // صراحةً بعد WebApplication.CreateBuilder(args) — أي مصدر يُضاف لاحقًا يفوز، فمتغيرات
            // البيئة (وليس معامل سطر أوامر --ConnectionStrings:... الذي يُغلَب لاحقًا بملف
            // appsettings.Development.json) هي الطريقة الوحيدة المضمونة لتجاوز سلسلة اتصال قاعدة
            // بيانات التطوير الحقيقية QdratNewDB والتأكيد الفعلي أن هذه العملية الفرعية تتصل
            // بقاعدة الاختبار المعزولة فقط.
            startInfo.Environment["ConnectionStrings__DefaultConnection"] = TestDatabaseBaseline.TestDatabaseConnectionString;

            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("تعذّر تشغيل عملية QdratNew الفرعية.");

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var ready = false;
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                {
                    var stderr = await process.StandardError.ReadToEndAsync();
                    throw new InvalidOperationException($"عملية QdratNew الفرعية أُنهيت مبكرًا (ExitCode={process.ExitCode}):\n{stderr}");
                }

                try
                {
                    var response = await httpClient.GetAsync($"{baseUrl}/LMS/login");
                    if ((int)response.StatusCode < 500)
                    {
                        ready = true;
                        break;
                    }
                }
                catch
                {
                    // السيرفر لم يبدأ الاستماع بعد — أعِد المحاولة.
                }

                await Task.Delay(500);
            }

            if (!ready)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("لم يبدأ خادم QdratNew الفرعي الاستماع خلال 60 ثانية.");
            }

            return new RealServerFixture(process, baseUrl);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                    await Task.Run(() => _process.WaitForExit(5000));
                }
                catch
                {
                    // أفضل مجهود فقط عند الإيقاف — لا يجب أن يُسقِط الاختبار.
                }
            }
            _process.Dispose();
        }
    }
}
